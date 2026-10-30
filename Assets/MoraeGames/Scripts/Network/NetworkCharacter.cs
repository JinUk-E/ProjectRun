using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using MoraeGames.Player;
using MoraeGames.Game;
using MoraeGames.Core;
using MoraeGames.Ability;

namespace MoraeGames.Network
{
    /// <summary>Owner input, server simulation, and passive client presentation.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkCharacter : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> _isAiControlled = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<byte> _pursuitState = new NetworkVariable<byte>(0,
            NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        /// <summary>Server-confirmed police attention, replicated only to this player's owner.</summary>
        public AICopController.PursuitState PursuitState => (AICopController.PursuitState)_pursuitState.Value;
        private PlayerController _player;
        private CopController _cop;
        private RobberController _robber;
        private AbilityHolder _ability;
        private Rigidbody _body;
        private Vector2 _moveInput;
        private float _lastInputTime;
        private float _nextAbilityRequest;
        private float _nextPing;

        /// <summary>Owner requests a team ping; the server supplies position, team and rate limit.</summary>
        public void RequestTeamPing()
        {
            if (!IsSpawned || !IsOwner || !MatchAllowsActions || InGameManager.IsPaused) return;
            TeamPingServerRpc();
        }

        [ServerRpc]
        private void TeamPingServerRpc()
        {
            if (!MatchAllowsActions || Time.unscaledTime < _nextPing || IsAiControlled) return;
            _nextPing = Time.unscaledTime + 1f;
            var targets = new System.Collections.Generic.List<ulong>();
            var room = NetworkRoomState.Instance;
            if (room == null || !room.Selections.TryGetValue(OwnerClientId, out var sender)) return;
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                if (room.Selections.TryGetValue(id, out var teammate) && teammate.Role == sender.Role) targets.Add(id);
            TeamPingClientRpc(transform.position, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = targets } });
        }

        [ClientRpc]
        private void TeamPingClientRpc(Vector3 position, ClientRpcParams targets = default)
        {
            MoraeGames.UI.PingSystem.Instance?.ReceiveTeamPing(position, "동료");
        }
        private uint _inputSequence;
        private uint _receivedSequence;
        private uint _processedSequence;
        private uint _snapshotSequence;
        private float _snapshotTimer;
        private float _lastSnapshotTime;
        private ClientRpcParams _ownerTarget;
        private ClientMovementPrediction _prediction;
        private Transform _visual;
        private Transform _cameraTarget;
        private Vector3 _visualLocalPosition;
        private Quaternion _visualLocalRotation;
        public bool IsAiControlled => _isAiControlled.Value;
        public Vector3 PresentedPosition => _prediction != null ? _prediction.VisualPosition : transform.position;
        public static bool MatchAllowsActions => GameManager.Instance != null &&
            GameManager.Instance.CurrentState == GameManager.GameState.Playing;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _cop = GetComponent<CopController>();
            _robber = GetComponent<RobberController>();
            _ability = GetComponent<AbilityHolder>();
            _body = GetComponent<Rigidbody>();
        }

        public void SetAiControlled(bool value) => _isAiControlled.Value = value;

        public override void OnNetworkSpawn()
        {
            var ai = GetComponent<AIController>();
            var agent = GetComponent<NavMeshAgent>();
            if (IsAiControlled)
            {
                _player.isLocalPlayer = false;
                if (!IsServer)
                {
                    if (ai != null) ai.enabled = false;
                    if (agent != null) agent.enabled = false;
                    _body.isKinematic = true;
                }
                return;
            }
            // Humans have no AI on any peer, including a dedicated server.
            if (ai != null) { ai.enabled = false; Destroy(ai); }
            if (agent != null) agent.enabled = false;
            _player.isLocalPlayer = IsOwner;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _body.isKinematic = !IsServer;
            _body.useGravity = false;
            _body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            if (IsServer) _body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            if (IsOwner)
            {
                var camera = FindFirstObjectByType<CameraFollow>();
                if (!IsServer)
                {
                    _visual = transform.Find("Body");
                    if (_visual != null)
                    {
                        _visualLocalPosition = _visual.localPosition;
                        _visualLocalRotation = _visual.localRotation;
                    }
                    _prediction = new ClientMovementPrediction(_player.MovementMotor, transform.position, transform.rotation, _player.moveSpeed);
                    _cameraTarget = new GameObject("PredictedCameraTarget").transform;
                    _cameraTarget.SetParent(transform, false);
                    _lastSnapshotTime = Time.unscaledTime;
                }
                if (camera != null) camera.target = _cameraTarget != null ? _cameraTarget : transform;
            }
            if (IsServer && !IsOwner)
                _ownerTarget = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };
        }

        public static Vector2 SanitizeInput(Vector2 input)
        {
            if (float.IsNaN(input.x) || float.IsNaN(input.y) ||
                float.IsInfinity(input.x) || float.IsInfinity(input.y)) return Vector2.zero;
            return Vector2.ClampMagnitude(input, 1f);
        }

        public void SubmitMovement(Vector2 input)
        {
            if (!IsSpawned || !IsOwner || IsAiControlled) return;
            input = SanitizeInput(input);
            uint sequence = ++_inputSequence;
            if (IsServer) AcceptMovement(input, sequence);
            else
            {
                if (_prediction != null && MatchAllowsActions && _player.enabled &&
                    (_robber == null || !_robber.IsArrested) && Time.unscaledTime - _lastSnapshotTime < 0.5f)
                    _prediction.Step(sequence, input, Time.fixedDeltaTime, _player.rotationSpeed);
                MovementServerRpc(input, sequence);
            }
        }

        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void MovementServerRpc(Vector2 input, uint sequence) => AcceptMovement(input, sequence);

        private void AcceptMovement(Vector2 input, uint sequence)
        {
            if (!ClientMovementPrediction.IsNewer(sequence, _receivedSequence)) return;
            _receivedSequence = sequence;
            _moveInput = SanitizeInput(input);
            _lastInputTime = Time.unscaledTime;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || IsAiControlled) return;
            _processedSequence = _receivedSequence;
            if (!MatchAllowsActions || !_player.enabled || (_robber != null && _robber.IsArrested))
            {
                _moveInput = Vector2.zero;
                if (!_body.isKinematic) _body.linearVelocity = Vector3.zero;
                return;
            }
            Vector2 input = Time.unscaledTime - _lastInputTime <= 0.25f ? _moveInput : Vector2.zero;
            _player.MoveAuthoritatively(new Vector3(input.x, 0f, input.y));
            if (_cop != null && !IsOwnedByServer) _cop.ServerAutoArrest();
        }

        private void LateUpdate()
        {
            if (!IsSpawned || IsAiControlled) return;
            if (IsServer)
            {
                _pursuitState.Value = (byte)(MatchAllowsActions ? AICopController.GetPursuitState(_robber) : AICopController.PursuitState.Clear);
                if (IsOwner) return;
                _snapshotTimer -= Time.deltaTime;
                if (_snapshotTimer > 0f) return;
                _snapshotTimer = 0.05f;
                bool blocked = !MatchAllowsActions || !_player.enabled || (_robber != null && _robber.IsArrested);
                // LateUpdate observes the completed physics step, not the queued MovePosition target.
                MovementSnapshotClientRpc(++_snapshotSequence, _processedSequence, _body.position, _body.rotation,
                    _player.moveSpeed, blocked, _ownerTarget);
                return;
            }
            if (!IsOwner || _prediction == null) return;
            _prediction.SmoothCorrection(Time.deltaTime);
            Vector3 position = _prediction.VisualPosition;
            if (_visual != null)
                _visual.SetPositionAndRotation(position + _prediction.Rotation * _visualLocalPosition,
                    _prediction.Rotation * _visualLocalRotation);
            if (_cameraTarget != null) _cameraTarget.position = position;
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void MovementSnapshotClientRpc(uint snapshot, uint acknowledged, Vector3 position, Quaternion rotation,
            float speed, bool blocked, ClientRpcParams target = default)
        {
            if (!IsOwner || IsServer || _prediction == null) return;
            if (_prediction.Reconcile(snapshot, acknowledged, position, rotation, speed, blocked, _player.rotationSpeed))
                _lastSnapshotTime = Time.unscaledTime;
        }

        public override void OnNetworkDespawn()
        {
            if (_visual != null)
            {
                _visual.localPosition = _visualLocalPosition;
                _visual.localRotation = _visualLocalRotation;
            }
            if (_cameraTarget != null) Destroy(_cameraTarget.gameObject);
            _prediction = null;
        }

        public void RequestArrest()
        {
            if (IsSpawned && IsOwner && !IsServer) ArrestRequestServerRpc();
        }

        [ServerRpc]
        private void ArrestRequestServerRpc()
        {
            if (MatchAllowsActions && _player.enabled && _cop != null) _cop.ServerTryArrest();
        }

        /// <summary>Server validates cooldown, match phase and arrest state.</summary>
        public bool RequestAbility()
        {
            if (!IsSpawned || (!IsOwner && !(IsServer && IsAiControlled))) return false;
            if (IsServer) return ActivateOnServer();
            if (Time.unscaledTime < _nextAbilityRequest) return false;
            _nextAbilityRequest = Time.unscaledTime + 0.2f;
            AbilityRequestServerRpc();
            return true;
        }

        [ServerRpc]
        private void AbilityRequestServerRpc() => ActivateOnServer();

        private bool ActivateOnServer()
        {
            if (!MatchAllowsActions || !_player.enabled || _ability == null || !_ability.IsReady ||
                (_robber != null && _robber.IsArrested)) return false;
            _ability.ActivateRemote();
            AbilityActivatedClientRpc();
            return true;
        }

        public void NotifyAbilityActivated() { }

        [ClientRpc]
        private void AbilityActivatedClientRpc()
        {
            if (!IsServer && _ability != null) _ability.ActivateRemote();
        }
    }
}
