using UnityEngine;
using System.Collections.Generic;
using MoraeGames.Network;

namespace MoraeGames.Ability
{
    /// <summary>
    /// MonoBehaviour that manages ability runtime state.
    /// Attach to a character and assign an AbilityDataSO asset.
    /// All timers and spawned objects live here (not in the SO).
    /// </summary>
    public class AbilityHolder : MonoBehaviour
    {
        [Header("Ability Asset")]
        public AbilityDataSO abilityData;

        // Runtime state
        private float cooldownTimer = 0f;
        private float activeTimer = 0f;
        private bool isActive = false;

        // Public accessors (same API as old AbilityBase for compatibility)
        public bool IsReady => cooldownTimer <= 0f && !isActive && abilityData != null;
        public bool IsActive => isActive;
        public float CooldownRemaining => cooldownTimer;
        public float CooldownPercent => (abilityData != null && abilityData.cooldown > 0)
            ? Mathf.Clamp01(cooldownTimer / abilityData.cooldown) : 0f;

        // Runtime storage for ability effects (spawned objects, saved values)
        public List<GameObject> spawnedObjects = new List<GameObject>();
        public Dictionary<string, float> savedValues = new Dictionary<string, float>();

        // Cached sibling for networked activation replay (null in offline prefabs is fine)
        private NetworkCharacter _networkCharacter;

        private void Awake()
        {
            _networkCharacter = GetComponent<NetworkCharacter>();
        }

        private void Update()
        {
            if (abilityData == null) return;

            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;

            if (isActive)
            {
                abilityData.OnActiveUpdate(this);

                activeTimer -= Time.deltaTime;
                if (activeTimer <= 0f)
                    Deactivate();
            }
        }

        public bool TryActivate()
        {
            if (!IsReady) return false;
            if (NetworkSessionManager.IsNetworked)
                return _networkCharacter != null && _networkCharacter.RequestAbility();

            ActivateInternal();

            // Replay the activation on every other machine (networked sessions only)
            if (_networkCharacter != null)
                _networkCharacter.NotifyAbilityActivated();
            return true;
        }

        /// <summary>
        /// Remote-triggered activation (no re-broadcast). Idempotent on the origin
        /// machine because its cooldown is already running.
        /// </summary>
        public void ActivateRemote()
        {
            if (!IsReady) return;
            ActivateInternal();
        }

        private void ActivateInternal()
        {
            isActive = true;
            activeTimer = abilityData.duration;
            cooldownTimer = GetEffectiveCooldown();
            abilityData.OnActivate(this);
            Debug.Log($"[Ability] {abilityData.abilityName} activated! Duration: {abilityData.duration}s");
        }

        public void Deactivate()
        {
            if (!isActive) return;
            isActive = false;
            activeTimer = 0f;
            abilityData.OnDeactivate(this);
            Debug.Log($"[Ability] {abilityData.abilityName} deactivated.");
        }

        /// <summary>Cleanup all spawned objects tracked by this holder.</summary>
        public void CleanupSpawnedObjects()
        {
            foreach (var obj in spawnedObjects)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedObjects.Clear();
        }

        /// <summary>Apply a cooldown reduction (from skill tree). Minimum 1s.</summary>
        public void ReduceCooldown(float amount)
        {
            if (abilityData != null)
            {
                // We can't modify SO at runtime, so adjust the timer offset
                // Store the reduction so we apply it each activation
                if (!savedValues.ContainsKey("cooldownReduction"))
                    savedValues["cooldownReduction"] = 0f;
                savedValues["cooldownReduction"] += amount;
            }
        }

        public float GetEffectiveCooldown()
        {
            if (abilityData == null) return 0f;
            float reduction = savedValues.ContainsKey("cooldownReduction") ? savedValues["cooldownReduction"] : 0f;
            return Mathf.Max(1f, abilityData.cooldown - reduction);
        }

        private void OnDestroy()
        {
            // Revert active effects (speed modifiers, disguise flag, ...) before cleanup
            Deactivate();
            CleanupSpawnedObjects();
        }
    }
}
