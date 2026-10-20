using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MoraeGames.Core;
using MoraeGames.Ability;

namespace MoraeGames.Player
{
    /// <summary>
    /// Base controller for player-controlled characters. Handles movement and ability activation.
    /// Movement speed is derived from a base value plus a multiplicative modifier stack, so
    /// concurrent effects (zones, buffs, debuffs) compose and revert safely in any order.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        private static readonly List<PlayerController> _instances = new List<PlayerController>();
        public static IReadOnlyList<PlayerController> Instances => _instances;
        public bool IsLocallyControlled => MoraeGames.Network.NetworkSessionManager.IsNetworked
            ? _networkCharacter != null && _networkCharacter.IsSpawned && _networkCharacter.IsOwner && !_networkCharacter.IsAiControlled
            : isLocalPlayer;
        [Header("Base Stats")]
        public float baseMoveSpeed = GameConstants.DefaultMoveSpeed;
        public float health = GameConstants.DefaultHealth;
        public bool isLocalPlayer = false;

        [Header("Rotation")]
        [Tooltip("Turn rate toward the movement direction, in degrees per second.")]
        public float rotationSpeed = 720f;

        /// <summary>Current movement speed after all modifiers. Use ApplySpeedModifier to change.</summary>
        public float moveSpeed { get; private set; }

        protected Rigidbody rb;
        protected AbilityHolder ability;

        // Multiplicative speed modifiers keyed by their source (zone, ability, debuff).
        private readonly Dictionary<object, float> _speedModifiers = new Dictionary<object, float>();
        private UnityEngine.AI.NavMeshAgent _navAgent;
        private float _baseNavSpeed;
        private MoraeGames.Network.NetworkCharacter _networkCharacter;
        public PlanarMovementMotor MovementMotor { get; private set; }

        protected virtual void Awake()
        {
            _instances.Add(this);
            if (GetComponent<MoraeGames.Game.LocalVisionSubject>() == null)
                gameObject.AddComponent<MoraeGames.Game.LocalVisionSubject>();
            rb = GetComponent<Rigidbody>();
            _networkCharacter = GetComponent<MoraeGames.Network.NetworkCharacter>();
            CapsuleCollider solid = null;
            foreach (var collider in GetComponentsInChildren<CapsuleCollider>())
                if (!collider.isTrigger) { solid = collider; break; }
            MovementMotor = new PlanarMovementMotor(transform, rb, solid);
            ability = GetComponent<AbilityHolder>();
            _navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (_navAgent != null) _baseNavSpeed = _navAgent.speed;
            RecalculateSpeed();
        }

        protected virtual void OnDestroy() => _instances.Remove(this);

        /// <summary>Registers (or updates) a multiplicative speed modifier under the given key.</summary>
        public void ApplySpeedModifier(object key, float multiplier)
        {
            _speedModifiers[key] = multiplier;
            RecalculateSpeed();
        }

        /// <summary>Removes the speed modifier registered under the given key. Safe to call twice.</summary>
        public void RemoveSpeedModifier(object key)
        {
            if (_speedModifiers.Remove(key))
                RecalculateSpeed();
        }

        /// <summary>Changes the base movement speed (e.g. role stats, skill tree) and reapplies modifiers.</summary>
        public void SetBaseMoveSpeed(float value)
        {
            baseMoveSpeed = value;
            RecalculateSpeed();
        }

        /// <summary>Changes the base NavMeshAgent speed and reapplies modifiers.</summary>
        public void SetBaseNavSpeed(float value)
        {
            _baseNavSpeed = value;
            RecalculateSpeed();
        }

        private void RecalculateSpeed()
        {
            float product = 1f;
            foreach (var multiplier in _speedModifiers.Values)
                product *= multiplier;

            moveSpeed = baseMoveSpeed * product;
            if (_navAgent != null)
                _navAgent.speed = _baseNavSpeed * product;
        }

        protected virtual void Update()
        {
            if (!isLocalPlayer) return;

            // Update still runs at timeScale 0 — block gameplay input while paused
            if (MoraeGames.Game.InGameManager.IsPaused) return;

            // Ability activation (Q key)
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                TryActivateAbility();
#else
            if (Input.GetKeyDown(KeyCode.Q))
                TryActivateAbility();
#endif

            // On-screen ability button (mobile)
            if (MobileInput.ConsumeAbilityPressed())
                TryActivateAbility();
        }

        private void TryActivateAbility()
        {
            if (ability != null && ability.IsReady)
            {
                ability.TryActivate();
            }
        }

        protected virtual void FixedUpdate()
        {
            if (!isLocalPlayer) return;
            HandleMovement();
        }

        protected virtual void HandleMovement()
        {
            float horizontal = 0f;
            float vertical = 0f;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical = 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical = -1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal = 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal = -1f;
            }
#else
            horizontal = Input.GetAxisRaw("Horizontal");
            vertical = Input.GetAxisRaw("Vertical");
#endif

            // Virtual joystick (mobile) takes priority while actively used
            Vector2 joystick = MobileInput.MoveDirection;
            if (joystick.sqrMagnitude > 0f)
            {
                horizontal = joystick.x;
                vertical = joystick.y;
            }

            // ClampMagnitude (not normalize) preserves analog joystick sensitivity
            Vector3 movement = Vector3.ClampMagnitude(new Vector3(horizontal, 0, vertical), 1f);
            if (MoraeGames.Game.InGameManager.IsPaused) movement = Vector3.zero;
            if (MoraeGames.Network.NetworkSessionManager.IsNetworked)
            {
                if (_networkCharacter != null) _networkCharacter.SubmitMovement(new Vector2(movement.x, movement.z));
                return;
            }
            MoveAuthoritatively(movement);
        }

        /// <summary>Moves using server-owned speed and physics; never accepts client positions.</summary>
        public void MoveAuthoritatively(Vector3 movement)
        {
            if (!MoraeGames.Network.NetworkSessionManager.HasStateAuthority || !enabled) return;
            if (this is RobberController robber && robber.IsArrested) return;
            if (rb.isKinematic) return;
            movement = Vector3.ClampMagnitude(movement, 1f);
            rb.linearVelocity = Vector3.zero;
            Vector3 newPosition = MovementMotor.Step(rb.position, new Vector2(movement.x, movement.z), moveSpeed, Time.fixedDeltaTime);
            rb.MovePosition(newPosition);

            // Face the movement direction (FreezeRotation only blocks physics-driven
            // rotation; MoveRotation still applies). AI characters rotate via NavMeshAgent.
            if (movement.sqrMagnitude > 0.0001f)
            {
                rb.MoveRotation(PlanarMovementMotor.Turn(rb.rotation, new Vector2(movement.x, movement.z), rotationSpeed, Time.fixedDeltaTime));
            }
        }

        /// <summary>Applies damage to this player. Calls Die() if health reaches zero.</summary>
        public virtual void TakeDamage(float amount)
        {
            health -= amount;
            if (health <= 0)
            {
                Die();
            }
        }

        protected virtual void Die()
        {
            Debug.Log($"{gameObject.name} died.");
        }
    }
}
