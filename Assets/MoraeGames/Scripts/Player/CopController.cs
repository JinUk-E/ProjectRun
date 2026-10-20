using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MoraeGames.Core;
using MoraeGames.Game;

namespace MoraeGames.Player
{
    /// <summary>
    /// Player-controlled cop with arrest capabilities.
    /// Uses physics overlap for proximity arrest detection.
    /// </summary>
    public class CopController : PlayerController
    {
        [Header("Cop Specific Stats")]
        public float arrestRange = GameConstants.CopArrestRange;

        private bool _tryArrestInput = false;

        // Reusable buffer for Physics.OverlapSphereNonAlloc (Rule §4: Zero-Allocation)
        // 32 slots: characters are compound colliders (2 each) and the prison area gets crowded
        private readonly Collider[] _overlapBuffer = new Collider[32];

        private MoraeGames.Network.NetworkCharacter _networkCharacter;

        protected override void Awake()
        {
            base.Awake();

            // Role stats are runtime-authoritative (asymmetric stat spec: cops are faster)
            SetBaseMoveSpeed(GameConstants.CopMoveSpeed);
            SetBaseNavSpeed(GameConstants.CopNavSpeed);

            _networkCharacter = GetComponent<MoraeGames.Network.NetworkCharacter>();
        }

        protected override void Update()
        {
            if (!isLocalPlayer) return;

            // Update still runs at timeScale 0 — block gameplay input while paused
            if (MoraeGames.Game.InGameManager.IsPaused) return;

            base.Update(); // Handles ability input (Q key)

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                _tryArrestInput = true;
#else
            if (Input.GetKeyDown(KeyCode.E))
                _tryArrestInput = true;
#endif

            // On-screen interact button doubles as the arrest button for cops
            if (MobileInput.ConsumeInteractPressed())
                _tryArrestInput = true;
        }

        protected override void FixedUpdate()
        {
            if (!isLocalPlayer) return;
            base.FixedUpdate();

            // Automatic arrest via proximity detection (supplements OnTriggerEnter)
            HandleAutomaticArrest();

            if (_tryArrestInput)
            {
                if (MoraeGames.Network.NetworkSessionManager.HasStateAuthority)
                    HandleArrest();
                else if (_networkCharacter != null)
                    _networkCharacter.RequestArrest(); // networked client: server judges
                _tryArrestInput = false;
            }
        }

        /// <summary>Server-side manual arrest for a remote human cop (relayed request).</summary>
        public void ServerTryArrest() => HandleArrest();

        /// <summary>Server-side proximity auto-arrest for a remote human cop (driven per physics tick).</summary>
        public void ServerAutoArrest() => HandleAutomaticArrest();

        private void HandleAutomaticArrest()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position + Vector3.up,
                GameConstants.CopAutoArrestRadius,
                _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                TryArrestTarget(_overlapBuffer[i].transform.root);
            }
        }

        private void HandleArrest()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                arrestRange,
                _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                TryArrestTarget(_overlapBuffer[i].transform.root);
            }
        }

        private void TryArrestTarget(Transform target)
        {
            if (!enabled || !MoraeGames.Network.NetworkCharacter.MatchAllowsActions) return;
            // Judgments run only on the state authority (offline or host).
            // Client-side arrests become a ServerRpc request in NGO Phase 3.
            if (!MoraeGames.Network.NetworkSessionManager.HasStateAuthority) return;

            if (!target.CompareTag("Robber")) return;
            if (!HeistBuilding.HasSight(transform.position, target.position)) return;

            IArrestable arrestable = target.GetComponent<IArrestable>();
            if (arrestable != null && !arrestable.IsArrested && !arrestable.IsInvincible)
            {
                Debug.Log($"[CopController] {gameObject.name} arrested {target.name}!");
                arrestable.OnArrested();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Unity delivers trigger events to disabled components: block arrests
            // during the ready freeze / after game end, and let AICopController
            // handle AI-driven cops (this path is for the local player only).
            if (!enabled || !isLocalPlayer) return;
            if (!MoraeGames.Network.NetworkSessionManager.HasStateAuthority) return;

            // Arrest on touch: check root, rigidbody, and direct tag
            Transform robberRoot = null;
            if (other.CompareTag("Robber"))
                robberRoot = other.transform.root;
            else if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Robber"))
                robberRoot = other.attachedRigidbody.transform;
            else if (other.transform.root.CompareTag("Robber"))
                robberRoot = other.transform.root;

            if (robberRoot == null) return;
            if (!HeistBuilding.HasSight(transform.position, robberRoot.position)) return;

            IArrestable arrestable = robberRoot.GetComponent<IArrestable>();
            if (arrestable != null && !arrestable.IsArrested && !arrestable.IsInvincible)
            {
                Debug.Log($"[CopController] {gameObject.name} touched and arrested {robberRoot.name}!");
                arrestable.OnArrested();
            }
        }
    }
}
