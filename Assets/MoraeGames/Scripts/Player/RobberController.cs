using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MoraeGames.Core;
using MoraeGames.Game;

namespace MoraeGames.Player
{
    /// <summary>
    /// Player-controlled robber with loot/rescue interaction and arrest/invincibility state.
    /// This component is the single owner of the robber's Arrested/Invincible/Disguised state;
    /// AIRobberController delegates its IArrestable implementation here.
    /// </summary>
    public class RobberController : PlayerController, IArrestable
    {
        [Header("Robber Specific Stats")]
        public float interactionRange = GameConstants.RobberInteractionRange;

        private bool _isArrested = false;
        public bool IsArrested => _isArrested;
        private bool _isInvincible = false;
        public bool IsInvincible => _isInvincible;
        private float _invincibilityTimer = 0f;

        /// <summary>Set by the Disguise ability. Hides this robber from AI detection.</summary>
        public bool IsDisguised { get; set; }

        private bool _tryInteractInput = false;
        private bool _isHoldingInteract = false;
        private LootSpot _currentLootTarget;
        private PrisonZone _currentPrisonZone;

        // Cached references (Rule §2: Dependency — cache in Awake)
        private AIRobberController _aiRobberController;
        private MoraeGames.Network.NetworkRobberSync _networkRobberSync;

        // Visual feedback for invincibility (tinted via MaterialPropertyBlock — Rule §4:
        // Renderer.material instantiates a material copy and leaks on mobile)
        private Renderer _bodyRenderer;

        // Reusable buffer for Physics.OverlapSphereNonAlloc (Rule §4: Zero-Allocation)
        // 32 slots: characters are compound colliders (2 each) and the prison area gets crowded
        private readonly Collider[] _overlapBuffer = new Collider[32];

        protected override void Awake()
        {
            base.Awake();

            // Role stats are runtime-authoritative (asymmetric stat spec)
            SetBaseMoveSpeed(GameConstants.RobberMoveSpeed);
            SetBaseNavSpeed(GameConstants.RobberNavSpeed);

            // Cache sibling components
            _aiRobberController = GetComponent<AIRobberController>();
            _networkRobberSync = GetComponent<MoraeGames.Network.NetworkRobberSync>();

            Transform body = transform.Find("Body");
            if (body != null)
            {
                _bodyRenderer = body.GetComponentInChildren<Renderer>();
            }
        }

        protected override void Update()
        {
            // Invincibility must tick for AI-driven robbers too (single state owner).
            // On networked clients the flag is mirrored from the server instead —
            // ticking a zero timer here would instantly cancel the replicated state.
            if (_isInvincible && MoraeGames.Network.NetworkSessionManager.HasStateAuthority)
            {
                _invincibilityTimer -= Time.deltaTime;
                if (_invincibilityTimer <= 0f)
                {
                    _isInvincible = false;
                    RendererTint.ClearColor(_bodyRenderer);
                }
            }

            if (!isLocalPlayer || _isArrested) return;

            // Update still runs at timeScale 0 — block gameplay input while paused
            if (InGameManager.IsPaused) return;
            _isHoldingInteract = false;

            base.Update(); // Handles ability input (Q key)

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.fKey.wasPressedThisFrame)
                    _tryInteractInput = true;
                _isHoldingInteract = Keyboard.current.fKey.isPressed;
            }
#else
            if (Input.GetKeyDown(KeyCode.F))
                _tryInteractInput = true;
            _isHoldingInteract = Input.GetKey(KeyCode.F);
#endif

            // On-screen interact button (mobile): tap starts, hold continues
            if (MobileInput.ConsumeInteractPressed())
                _tryInteractInput = true;
            _isHoldingInteract = _isHoldingInteract || MobileInput.InteractHeld;
        }

        protected override void FixedUpdate()
        {
            if (!isLocalPlayer || _isArrested) return;

            base.FixedUpdate();
            if (InGameManager.IsPaused)
            {
                _tryInteractInput = false;
                _isHoldingInteract = false;
            }

            if (MoraeGames.Network.NetworkSessionManager.HasStateAuthority)
            {
                if (_tryInteractInput)
                {
                    HandleInteraction();
                    _tryInteractInput = false;
                }

                // Continue robbery or rescue while holding F
                if (_isHoldingInteract)
                {
                    ContinueInteraction();
                }
                else
                {
                    CancelCurrentInteraction();
                }
            }
            else
            {
                // Networked client: judgments live on the server — relay the input
                if (_networkRobberSync != null)
                    _networkRobberSync.SubmitInteractionInput(_tryInteractInput, _isHoldingInteract);
                _tryInteractInput = false;
            }
        }

        private void HandleInteraction()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, interactionRange, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                // Check for loot spot
                LootSpot loot = _overlapBuffer[i].GetComponent<LootSpot>();
                if (loot != null && !loot.IsLooted && loot.TryStartRobbery(gameObject))
                {
                    _currentLootTarget = loot;
                    Debug.Log($"{gameObject.name} started robbing {loot.gameObject.name}!");
                    return;
                }

                // Check for prison zone (rescue)
                PrisonZone prison = _overlapBuffer[i].GetComponent<PrisonZone>();
                if (prison != null && prison.TryStartRescue(gameObject))
                {
                    _currentPrisonZone = prison;
                    Debug.Log($"{gameObject.name} started rescue at prison!");
                    return;
                }
            }
        }

        private void ContinueInteraction()
        {
            if (_currentLootTarget != null)
            {
                float dist = Vector3.Distance(transform.position, _currentLootTarget.transform.position);
                if (dist > interactionRange * 1.5f)
                {
                    CancelCurrentInteraction();
                    return;
                }
                _currentLootTarget.UpdateRobbery(Time.fixedDeltaTime);
                if (_currentLootTarget.IsLooted)
                {
                    _currentLootTarget = null;
                }
            }
            else if (_currentPrisonZone != null)
            {
                float dist = Vector3.Distance(transform.position, _currentPrisonZone.transform.position);
                if (dist > GameConstants.PrisonZoneRadius * 1.5f)
                {
                    CancelCurrentInteraction();
                    return;
                }
                _currentPrisonZone.UpdateRescue(Time.fixedDeltaTime);
                if (!_currentPrisonZone.IsBeingRescued)
                {
                    _currentPrisonZone = null;
                }
            }
        }

        private void CancelCurrentInteraction()
        {
            if (_currentLootTarget != null)
            {
                _currentLootTarget.CancelRobbery();
                _currentLootTarget = null;
            }
            if (_currentPrisonZone != null)
            {
                _currentPrisonZone.CancelRescue();
                _currentPrisonZone = null;
            }
        }

        /// <summary>
        /// Called when this robber is arrested by a cop. Single entry point for arrest state.
        /// </summary>
        public void OnArrested()
        {
            if (_isArrested || _isInvincible) return;
            _isArrested = true;

            // An in-progress rescue/robbery must not stay locked by an arrested robber
            CancelCurrentInteraction();

            // Physical/AI side effects live in the AI component (NavMeshAgent owner)
            if (_aiRobberController != null)
                _aiRobberController.HandleArrestedState();

            PrisonHelper.TeleportToPrison(transform, rb);

            if (GameManager.Instance != null)
                GameManager.Instance.BroadcastSystemMessage("도둑 체포! 감옥에서 동료를 구출할 수 있어요", Color.cyan);

            Debug.Log($"[RobberController] {gameObject.name} arrested and moved to prison.");
        }

        /// <summary>
        /// Called when this robber is rescued from prison. Grants temporary invincibility.
        /// </summary>
        public void OnRescued()
        {
            if (!_isArrested) return;
            _isArrested = false;

            // Grant temporary invincibility
            _isInvincible = true;
            _invincibilityTimer = GameConstants.InvincibilityDuration;
            RendererTint.SetColor(_bodyRenderer, new Color(1f, 1f, 1f, 0.5f)); // White flash

            if (_aiRobberController != null)
                _aiRobberController.HandleRescuedState();

            Debug.Log($"[RobberController] {gameObject.name} rescued with {GameConstants.InvincibilityDuration}s invincibility!");
        }

        /// <summary>
        /// Server-side drive for a remote human robber's relayed interaction input.
        /// Runs the exact local Handle/Continue/Cancel logic against the replicated
        /// character position (called from NetworkRobberSync.FixedUpdate).
        /// </summary>
        public void ServerDriveInteraction(bool pressed, bool held)
        {
            if (!enabled || !MoraeGames.Network.NetworkCharacter.MatchAllowsActions || _isArrested)
            {
                CancelCurrentInteraction();
                return;
            }

            if (pressed) HandleInteraction();
            if (held) ContinueInteraction();
            else CancelCurrentInteraction();
        }

        /// <summary>
        /// Networked clients: mirrors server-replicated state WITHOUT gameplay side
        /// effects (no teleport, no interaction cancel — the server already ran those;
        /// position arrives via server transform replication).
        /// Input locking falls out naturally: Update/FixedUpdate early-return on arrest.
        /// </summary>
        public void ApplyReplicatedState(bool arrested, bool invincible, bool disguised)
        {
            _isArrested = arrested;
            _isInvincible = invincible;
            IsDisguised = disguised;

            if (invincible)
                RendererTint.SetColor(_bodyRenderer, new Color(1f, 1f, 1f, 0.5f));
            else if (disguised)
                RendererTint.SetColor(_bodyRenderer, new Color(0.3f, 0.3f, 0.9f));
            else
                RendererTint.ClearColor(_bodyRenderer);
        }
    }
}

