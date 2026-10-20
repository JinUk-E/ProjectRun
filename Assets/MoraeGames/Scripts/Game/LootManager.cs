using UnityEngine;
using MoraeGames.Core;

namespace MoraeGames.Game
{
    /// <summary>
    /// Tracks total and stolen loot values across all loot spots in the scene.
    /// </summary>
    public class LootManager : MonoBehaviour
    {
        public int totalLootValue { get; private set; } = 0;
        public int stolenLootValue { get; private set; } = 0;

        private LootSpot[] allLootSpots;

        private void Start()
        {
            allLootSpots = FindObjectsByType<LootSpot>(FindObjectsSortMode.None);
            totalLootValue = 0;
            foreach (var spot in allLootSpots)
            {
                totalLootValue += spot.lootValue;
            }
            Debug.Log($"[LootManager] Total loot value: {totalLootValue}");
        }

        /// <summary>Registers a stolen loot value from a loot spot.</summary>
        public void RegisterStolen(int value, GameObject robber)
        {
            stolenLootValue += value;
            Debug.Log($"[LootManager] Loot stolen: {value} by {robber?.name}. Total stolen: {stolenLootValue}/{totalLootValue}");
        }

        /// <summary>Returns the fraction of total loot that has been stolen (0-1).</summary>
        public float GetStolenPercentage()
        {
            if (totalLootValue <= 0) return 0f;
            return (float)stolenLootValue / totalLootValue;
        }

        /// <summary>Whether the robbers have stolen enough loot to win.</summary>
        public bool IsLootWinConditionMet()
        {
            return GetStolenPercentage() >= GameConstants.LootWinThreshold;
        }

        /// <summary>Returns the nearest unlooted spot to the given position.</summary>
        public LootSpot GetNearestUnlootedSpot(Vector3 position)
        {
            if (allLootSpots == null) return null;

            LootSpot nearest = null;
            float minDist = float.MaxValue;

            foreach (var spot in allLootSpots)
            {
                if (spot.IsLooted || spot.IsBeingRobbed) continue;

                float dist = Vector3.Distance(position, spot.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = spot;
                }
            }
            return nearest;
        }
    }
}
