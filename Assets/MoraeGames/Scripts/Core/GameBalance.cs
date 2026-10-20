using UnityEngine;

namespace MoraeGames.Core
{
    /// <summary>
    /// Single source of truth for shipped balance values, as plain compile-time
    /// constants. GameBalance fields initialize from these, and GameConstants falls
    /// back to these when no GameBalance asset is loaded — so all three layers can
    /// never drift apart. Pure C# on purpose: these are safely readable from
    /// MonoBehaviour field initializers on loading threads.
    /// </summary>
    public static class GameBalanceDefaults
    {
        // Team Composition
        public const int CopCount = 3;
        public const int RobberCount = 5;

        // Cop Stats (police have the speed advantage per spec)
        public const float CopArrestRange = 2f;
        public const float CopAutoArrestRadius = 1.2f;
        public const float CopDetectionRadius = 15f;
        public const float CopNavSpeed = 5.5f;
        public const float CopMoveSpeed = 5.5f;

        // Robber Stats
        public const float RobberDetectionRadius = 15f;
        public const float RobberSafeDistance = 25f;
        public const float RobberNavSpeed = 5.0f;
        public const float RobberMoveSpeed = 5f;
        public const float RobberInteractionRange = 2f;
        public const float RobberFleeDistance = 10f;
        public const float RobberRescueDecisionInterval = 2f;
        public const float RobberRescueAttemptChance = 0.4f;

        // AI
        public const float WanderTimeout = 5f;
        public const float CopWanderRadius = 15f;
        public const float RobberWanderRadius = 20f;
        public const float ChaseAbandonMultiplier = 1.5f;

        // Player Defaults
        public const float DefaultMoveSpeed = 5f;
        public const float DefaultHealth = 100f;

        // Loot System
        public const int LootSpotCount = 6;
        public const float LootRobberyDuration = 4f;
        public const float LootWinThreshold = 0.6f;
        public const int LootValuePerSpot = 100;

        // Rescue System
        public const float RescueDuration = 3f;
        public const float PrisonZoneRadius = 5f;
        public const float InvincibilityDuration = 3f;

        // Ability - Cop
        public const float CopSpeedBoostMultiplier = 1.5f;
        public const float CopSpeedBoostDuration = 3f;
        public const float CopSpeedBoostCooldown = 15f;
        public const float CopAreaScanRadius = 30f;
        public const float CopAreaScanDuration = 2f;
        public const float CopAreaScanCooldown = 20f;
        public const float CCProjectileSpeed = 20f;
        public const float CCProjectileCooldown = 12f;
        public const float CCSlowDuration = 3f;
        public const float CCSlowMultiplier = 0.4f;
        public const float PrisonLockdownDuration = 8f;
        public const float PrisonLockdownCooldown = 30f;
        public const float PrisonLockdownRadius = 7f;

        // Ability - Robber
        public const float RobberSpeedBurstMultiplier = 2f;
        public const float RobberSpeedBurstDuration = 2f;
        public const float RobberSpeedBurstCooldown = 12f;
        public const float RobberSmokeBombDuration = 5f;
        public const float RobberSmokeBombCooldown = 25f;
        public const float RobberSmokeBombRadius = 5f;
        public const float RobberDisguiseDuration = 5f;
        public const float RobberDisguiseCooldown = 30f;
        public const float DecoyCloneDuration = 6f;
        public const float DecoyCloneCooldown = 20f;
        public const float DecoyCloneSpeed = 3f;

        // Map Gimmicks
        public const float CoopSwitchDoorDuration = 5f;
        public const float BoostPadMultiplier = 2f;
        public const float MudTrapMultiplier = 0.5f;

        // Game Flow
        public const float ReadyCountdownDuration = 3f;
        public const float GameTime = 180f;

        // UI
        public const float SystemMessageDuration = 2f;
        public const float PingDuration = 3f;
    }

    /// <summary>
    /// All gameplay-tunable balance values as a ScriptableObject (Rule §2: design data
    /// lives in SOs, not code). Field defaults come from GameBalanceDefaults.
    /// Create the editable asset via "MoraeGames/Setup Game Balance Asset";
    /// it must live at Resources/GameBalance to be picked up.
    /// </summary>
    [CreateAssetMenu(menuName = "MoraeGames/GameBalance")]
    public class GameBalance : ScriptableObject
    {
        [Header("Team Composition")]
        public int copCount = GameBalanceDefaults.CopCount;
        public int robberCount = GameBalanceDefaults.RobberCount;

        [Header("Cop Stats (police have the speed advantage per spec)")]
        public float copArrestRange = GameBalanceDefaults.CopArrestRange;
        public float copAutoArrestRadius = GameBalanceDefaults.CopAutoArrestRadius;
        public float copDetectionRadius = GameBalanceDefaults.CopDetectionRadius;
        public float copNavSpeed = GameBalanceDefaults.CopNavSpeed;
        public float copMoveSpeed = GameBalanceDefaults.CopMoveSpeed;

        [Header("Robber Stats")]
        public float robberDetectionRadius = GameBalanceDefaults.RobberDetectionRadius;
        public float robberSafeDistance = GameBalanceDefaults.RobberSafeDistance;
        public float robberNavSpeed = GameBalanceDefaults.RobberNavSpeed;
        public float robberMoveSpeed = GameBalanceDefaults.RobberMoveSpeed;
        public float robberInteractionRange = GameBalanceDefaults.RobberInteractionRange;
        public float robberFleeDistance = GameBalanceDefaults.RobberFleeDistance;
        public float robberRescueDecisionInterval = GameBalanceDefaults.RobberRescueDecisionInterval;
        [Range(0f, 1f)] public float robberRescueAttemptChance = GameBalanceDefaults.RobberRescueAttemptChance;

        [Header("AI")]
        public float wanderTimeout = GameBalanceDefaults.WanderTimeout;
        public float copWanderRadius = GameBalanceDefaults.CopWanderRadius;
        public float robberWanderRadius = GameBalanceDefaults.RobberWanderRadius;
        public float chaseAbandonMultiplier = GameBalanceDefaults.ChaseAbandonMultiplier;

        [Header("Player Defaults")]
        public float defaultMoveSpeed = GameBalanceDefaults.DefaultMoveSpeed;
        public float defaultHealth = GameBalanceDefaults.DefaultHealth;

        [Header("Loot System")]
        public int lootSpotCount = GameBalanceDefaults.LootSpotCount;
        public float lootRobberyDuration = GameBalanceDefaults.LootRobberyDuration;
        [Range(0f, 1f)] public float lootWinThreshold = GameBalanceDefaults.LootWinThreshold;
        public int lootValuePerSpot = GameBalanceDefaults.LootValuePerSpot;

        [Header("Rescue System")]
        public float rescueDuration = GameBalanceDefaults.RescueDuration;
        public float prisonZoneRadius = GameBalanceDefaults.PrisonZoneRadius;
        public float invincibilityDuration = GameBalanceDefaults.InvincibilityDuration;

        [Header("Ability - Cop Speed Boost")]
        public float copSpeedBoostMultiplier = GameBalanceDefaults.CopSpeedBoostMultiplier;
        public float copSpeedBoostDuration = GameBalanceDefaults.CopSpeedBoostDuration;
        public float copSpeedBoostCooldown = GameBalanceDefaults.CopSpeedBoostCooldown;

        [Header("Ability - Cop Area Scan")]
        public float copAreaScanRadius = GameBalanceDefaults.CopAreaScanRadius;
        public float copAreaScanDuration = GameBalanceDefaults.CopAreaScanDuration;
        public float copAreaScanCooldown = GameBalanceDefaults.CopAreaScanCooldown;

        [Header("Ability - Cop CC Projectile")]
        public float ccProjectileSpeed = GameBalanceDefaults.CCProjectileSpeed;
        public float ccProjectileCooldown = GameBalanceDefaults.CCProjectileCooldown;
        public float ccSlowDuration = GameBalanceDefaults.CCSlowDuration;
        [Range(0.05f, 1f)] public float ccSlowMultiplier = GameBalanceDefaults.CCSlowMultiplier;

        [Header("Ability - Cop Prison Lockdown")]
        public float prisonLockdownDuration = GameBalanceDefaults.PrisonLockdownDuration;
        public float prisonLockdownCooldown = GameBalanceDefaults.PrisonLockdownCooldown;
        public float prisonLockdownRadius = GameBalanceDefaults.PrisonLockdownRadius;

        [Header("Ability - Robber Speed Burst")]
        public float robberSpeedBurstMultiplier = GameBalanceDefaults.RobberSpeedBurstMultiplier;
        public float robberSpeedBurstDuration = GameBalanceDefaults.RobberSpeedBurstDuration;
        public float robberSpeedBurstCooldown = GameBalanceDefaults.RobberSpeedBurstCooldown;

        [Header("Ability - Robber Smoke Bomb")]
        public float robberSmokeBombDuration = GameBalanceDefaults.RobberSmokeBombDuration;
        public float robberSmokeBombCooldown = GameBalanceDefaults.RobberSmokeBombCooldown;
        public float robberSmokeBombRadius = GameBalanceDefaults.RobberSmokeBombRadius;

        [Header("Ability - Robber Disguise")]
        public float robberDisguiseDuration = GameBalanceDefaults.RobberDisguiseDuration;
        public float robberDisguiseCooldown = GameBalanceDefaults.RobberDisguiseCooldown;

        [Header("Ability - Robber Decoy Clone")]
        public float decoyCloneDuration = GameBalanceDefaults.DecoyCloneDuration;
        public float decoyCloneCooldown = GameBalanceDefaults.DecoyCloneCooldown;
        public float decoyCloneSpeed = GameBalanceDefaults.DecoyCloneSpeed;

        [Header("Map Gimmicks")]
        public float coopSwitchDoorDuration = GameBalanceDefaults.CoopSwitchDoorDuration;
        public float boostPadMultiplier = GameBalanceDefaults.BoostPadMultiplier;
        [Range(0.05f, 1f)] public float mudTrapMultiplier = GameBalanceDefaults.MudTrapMultiplier;

        [Header("Game Flow")]
        public float readyCountdownDuration = GameBalanceDefaults.ReadyCountdownDuration;
        public float gameTime = GameBalanceDefaults.GameTime;

        [Header("UI")]
        public float systemMessageDuration = GameBalanceDefaults.SystemMessageDuration;
        public float pingDuration = GameBalanceDefaults.PingDuration;
    }
}
