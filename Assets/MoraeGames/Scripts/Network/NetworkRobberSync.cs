using UnityEngine;
using Unity.Netcode;
using MoraeGames.Player;

namespace MoraeGames.Network
{
    /// <summary>
    /// Replicates the robber's authoritative state (arrested / invincible / disguised)
    /// from the server to every machine. The server-side RobberController remains the
    /// single source of truth (its gameplay side effects run there); clients only
    /// mirror flags and visuals via ApplyReplicatedState.
    ///
    /// Positions, including arrest/rescue teleports, are replicated by the server transform.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkRobberSync : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> _arrested = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<bool> _invincible = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<bool> _disguised = new NetworkVariable<bool>(false);

        // Press and hold travel together so message ordering cannot cancel a new interaction.
        private bool _interactHeld;

        private bool _pendingInteractPress;
        private float _lastInteractionInput;
        private RobberController _robber;

        private void Awake()
        {
            _robber = GetComponent<RobberController>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                _arrested.OnValueChanged += OnStateChanged;
                _invincible.OnValueChanged += OnStateChanged;
                _disguised.OnValueChanged += OnStateChanged;
                ApplyToLocalController();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
            {
                _arrested.OnValueChanged -= OnStateChanged;
                _invincible.OnValueChanged -= OnStateChanged;
                _disguised.OnValueChanged -= OnStateChanged;
            }
        }

        private void OnStateChanged(bool previous, bool current) => ApplyToLocalController();

        private void ApplyToLocalController()
        {
            if (_robber != null)
                _robber.ApplyReplicatedState(_arrested.Value, _invincible.Value, _disguised.Value);
        }

        // LateUpdate so same-frame judgment + teleport (arrest, rescue exit) have
        // already updated the server-side transform before we snapshot it
        private void LateUpdate()
        {
            if (!IsSpawned || !IsServer || _robber == null) return;

            if (_arrested.Value != _robber.IsArrested)
            {
                _arrested.Value = _robber.IsArrested;

            }

            if (_invincible.Value != _robber.IsInvincible)
                _invincible.Value = _robber.IsInvincible;

            if (_disguised.Value != _robber.IsDisguised)
                _disguised.Value = _robber.IsDisguised;
        }

        /// <summary>
        /// Owner-side: forwards this frame's interaction input to the server, which
        /// runs the actual judgment (RobberController.ServerDriveInteraction) against
        /// the replicated character position.
        /// </summary>
        public void SubmitInteractionInput(bool pressed, bool held)
        {
            if (!IsSpawned || !IsOwner) return;

            InteractionInputServerRpc(pressed, held);
        }

        [ServerRpc]
        private void InteractionInputServerRpc(bool pressed, bool held)
        {
            _interactHeld = held;
            _pendingInteractPress |= pressed;
            _lastInteractionInput = Time.unscaledTime;
        }

        // FixedUpdate for exact rate parity with the local interaction path
        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || _robber == null) return;
            if (IsOwnedByServer) return; // host's own robber and AI bots drive themselves

            _robber.ServerDriveInteraction(_pendingInteractPress,
                Time.unscaledTime - _lastInteractionInput <= 0.25f && _interactHeld);
            _pendingInteractPress = false;
        }

    }
}
