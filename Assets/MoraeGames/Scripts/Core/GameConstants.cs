using UnityEngine;

namespace MoraeGames.Core
{
    /// <summary>
    /// Facade over the GameBalance ScriptableObject (Resources/GameBalance).
    /// Call sites keep using GameConstants.X unchanged; values are designer-tunable
    /// in the asset.
    ///
    /// THREADING CONTRACT: property getters are read from MonoBehaviour field
    /// initializers during deserialization on loading threads, where Unity APIs
    /// (Resources.Load, CreateInstance) are forbidden. Therefore getters only read
    /// the cached reference and fall back to GameBalanceDefaults constants; loading
    /// happens exclusively on the main thread via the init hooks below.
    /// </summary>
    public static class GameConstants
    {
        private static GameBalance _balance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PreloadBalanceRuntime() => ReloadBalance();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void PreloadBalanceEditor() => ReloadBalance();
#endif

        /// <summary>(Re)loads the balance asset. Main thread only.</summary>
        public static void ReloadBalance()
        {
            _balance = Resources.Load<GameBalance>("GameBalance");
        }

        // ReferenceEquals (not Unity's overloaded ==) so this is safe off the main thread
        private static bool Loaded => !ReferenceEquals(_balance, null);

        // Prison (scene position is injected via PrisonHelper; this is the last-resort fallback)
        public static readonly Vector3 PrisonPosition = new Vector3(18f, 0.5f, 18f);

        // NavMesh area index reserved for robber-only vent passages.
        // AI cops exclude this bit from their areaMask; robbers keep AllAreas.
        public const int VentNavMeshArea = 3;

        // Game Rules
        public static int CopCount => Loaded ? _balance.copCount : GameBalanceDefaults.CopCount;
        public static int RobberCount => Loaded ? _balance.robberCount : GameBalanceDefaults.RobberCount;

        // Cop Stats
        public static float CopArrestRange => Loaded ? _balance.copArrestRange : GameBalanceDefaults.CopArrestRange;
        public static float CopAutoArrestRadius => Loaded ? _balance.copAutoArrestRadius : GameBalanceDefaults.CopAutoArrestRadius;
        public static float CopDetectionRadius => Loaded ? _balance.copDetectionRadius : GameBalanceDefaults.CopDetectionRadius;
        public static float CopNavSpeed => Loaded ? _balance.copNavSpeed : GameBalanceDefaults.CopNavSpeed;
        public static float CopMoveSpeed => Loaded ? _balance.copMoveSpeed : GameBalanceDefaults.CopMoveSpeed;

        // Robber Stats
        public static float RobberDetectionRadius => Loaded ? _balance.robberDetectionRadius : GameBalanceDefaults.RobberDetectionRadius;
        public static float RobberSafeDistance => Loaded ? _balance.robberSafeDistance : GameBalanceDefaults.RobberSafeDistance;
        public static float RobberNavSpeed => Loaded ? _balance.robberNavSpeed : GameBalanceDefaults.RobberNavSpeed;
        public static float RobberMoveSpeed => Loaded ? _balance.robberMoveSpeed : GameBalanceDefaults.RobberMoveSpeed;
        public static float RobberInteractionRange => Loaded ? _balance.robberInteractionRange : GameBalanceDefaults.RobberInteractionRange;
        public static float RobberFleeDistance => Loaded ? _balance.robberFleeDistance : GameBalanceDefaults.RobberFleeDistance;
        public static float RobberRescueDecisionInterval => Loaded ? _balance.robberRescueDecisionInterval : GameBalanceDefaults.RobberRescueDecisionInterval;
        public static float RobberRescueAttemptChance => Loaded ? _balance.robberRescueAttemptChance : GameBalanceDefaults.RobberRescueAttemptChance;

        // AI
        public static float WanderTimeout => Loaded ? _balance.wanderTimeout : GameBalanceDefaults.WanderTimeout;
        public static float CopWanderRadius => Loaded ? _balance.copWanderRadius : GameBalanceDefaults.CopWanderRadius;
        public static float RobberWanderRadius => Loaded ? _balance.robberWanderRadius : GameBalanceDefaults.RobberWanderRadius;
        public static float ChaseAbandonMultiplier => Loaded ? _balance.chaseAbandonMultiplier : GameBalanceDefaults.ChaseAbandonMultiplier;

        // Player
        public static float DefaultMoveSpeed => Loaded ? _balance.defaultMoveSpeed : GameBalanceDefaults.DefaultMoveSpeed;
        public static float DefaultHealth => Loaded ? _balance.defaultHealth : GameBalanceDefaults.DefaultHealth;

        // Loot System
        public static int LootSpotCount => Loaded ? _balance.lootSpotCount : GameBalanceDefaults.LootSpotCount;
        public static float LootRobberyDuration => Loaded ? _balance.lootRobberyDuration : GameBalanceDefaults.LootRobberyDuration;
        public static float LootWinThreshold => Loaded ? _balance.lootWinThreshold : GameBalanceDefaults.LootWinThreshold;
        public static int LootValuePerSpot => Loaded ? _balance.lootValuePerSpot : GameBalanceDefaults.LootValuePerSpot;

        // Rescue System
        public static float RescueDuration => Loaded ? _balance.rescueDuration : GameBalanceDefaults.RescueDuration;
        public static float PrisonZoneRadius => Loaded ? _balance.prisonZoneRadius : GameBalanceDefaults.PrisonZoneRadius;
        public static float InvincibilityDuration => Loaded ? _balance.invincibilityDuration : GameBalanceDefaults.InvincibilityDuration;

        // Ability System
        public static float CopSpeedBoostMultiplier => Loaded ? _balance.copSpeedBoostMultiplier : GameBalanceDefaults.CopSpeedBoostMultiplier;
        public static float CopSpeedBoostDuration => Loaded ? _balance.copSpeedBoostDuration : GameBalanceDefaults.CopSpeedBoostDuration;
        public static float CopSpeedBoostCooldown => Loaded ? _balance.copSpeedBoostCooldown : GameBalanceDefaults.CopSpeedBoostCooldown;
        public static float CopAreaScanRadius => Loaded ? _balance.copAreaScanRadius : GameBalanceDefaults.CopAreaScanRadius;
        public static float CopAreaScanDuration => Loaded ? _balance.copAreaScanDuration : GameBalanceDefaults.CopAreaScanDuration;
        public static float CopAreaScanCooldown => Loaded ? _balance.copAreaScanCooldown : GameBalanceDefaults.CopAreaScanCooldown;
        public static float RobberSpeedBurstMultiplier => Loaded ? _balance.robberSpeedBurstMultiplier : GameBalanceDefaults.RobberSpeedBurstMultiplier;
        public static float RobberSpeedBurstDuration => Loaded ? _balance.robberSpeedBurstDuration : GameBalanceDefaults.RobberSpeedBurstDuration;
        public static float RobberSpeedBurstCooldown => Loaded ? _balance.robberSpeedBurstCooldown : GameBalanceDefaults.RobberSpeedBurstCooldown;
        public static float RobberSmokeBombDuration => Loaded ? _balance.robberSmokeBombDuration : GameBalanceDefaults.RobberSmokeBombDuration;
        public static float RobberSmokeBombCooldown => Loaded ? _balance.robberSmokeBombCooldown : GameBalanceDefaults.RobberSmokeBombCooldown;
        public static float RobberSmokeBombRadius => Loaded ? _balance.robberSmokeBombRadius : GameBalanceDefaults.RobberSmokeBombRadius;
        public static float RobberDisguiseDuration => Loaded ? _balance.robberDisguiseDuration : GameBalanceDefaults.RobberDisguiseDuration;
        public static float RobberDisguiseCooldown => Loaded ? _balance.robberDisguiseCooldown : GameBalanceDefaults.RobberDisguiseCooldown;

        // CC Projectile (Cop)
        public static float CCProjectileSpeed => Loaded ? _balance.ccProjectileSpeed : GameBalanceDefaults.CCProjectileSpeed;
        public static float CCProjectileCooldown => Loaded ? _balance.ccProjectileCooldown : GameBalanceDefaults.CCProjectileCooldown;
        public static float CCSlowDuration => Loaded ? _balance.ccSlowDuration : GameBalanceDefaults.CCSlowDuration;
        public static float CCSlowMultiplier => Loaded ? _balance.ccSlowMultiplier : GameBalanceDefaults.CCSlowMultiplier;

        // Prison Lockdown (Cop)
        public static float PrisonLockdownDuration => Loaded ? _balance.prisonLockdownDuration : GameBalanceDefaults.PrisonLockdownDuration;
        public static float PrisonLockdownCooldown => Loaded ? _balance.prisonLockdownCooldown : GameBalanceDefaults.PrisonLockdownCooldown;
        public static float PrisonLockdownRadius => Loaded ? _balance.prisonLockdownRadius : GameBalanceDefaults.PrisonLockdownRadius;

        // Decoy Clone (Robber)
        public static float DecoyCloneDuration => Loaded ? _balance.decoyCloneDuration : GameBalanceDefaults.DecoyCloneDuration;
        public static float DecoyCloneCooldown => Loaded ? _balance.decoyCloneCooldown : GameBalanceDefaults.DecoyCloneCooldown;
        public static float DecoyCloneSpeed => Loaded ? _balance.decoyCloneSpeed : GameBalanceDefaults.DecoyCloneSpeed;

        // Map Gimmicks
        public static float CoopSwitchDoorDuration => Loaded ? _balance.coopSwitchDoorDuration : GameBalanceDefaults.CoopSwitchDoorDuration;
        public static float BoostPadMultiplier => Loaded ? _balance.boostPadMultiplier : GameBalanceDefaults.BoostPadMultiplier;
        public static float MudTrapMultiplier => Loaded ? _balance.mudTrapMultiplier : GameBalanceDefaults.MudTrapMultiplier;

        // Game State
        public static float ReadyCountdownDuration => Loaded ? _balance.readyCountdownDuration : GameBalanceDefaults.ReadyCountdownDuration;
        public static float GameTime => Loaded ? _balance.gameTime : GameBalanceDefaults.GameTime;

        // UI
        public static float SystemMessageDuration => Loaded ? _balance.systemMessageDuration : GameBalanceDefaults.SystemMessageDuration;
        public static float PingDuration => Loaded ? _balance.pingDuration : GameBalanceDefaults.PingDuration;
    }
}
