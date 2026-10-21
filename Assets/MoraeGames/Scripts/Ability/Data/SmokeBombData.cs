using UnityEngine;
using System.Collections.Generic;
using MoraeGames.Core;

namespace MoraeGames.Ability
{
    [CreateAssetMenu(menuName = "Abilities/Robber/SmokeBomb")]
    public class SmokeBombData : AbilityDataSO
    {
        [Header("Smoke Bomb")]
        public float smokeRadius = 5f;
        public Color smokeColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        private const string PoolKey = "SmokeBomb";

        public override void OnActivate(AbilityHolder holder)
        {
            // Pooled smoke cloud (Rule §4: pooling mandatory for effects)
            GameObject smokeEffect = EffectPool.Get(PoolKey, CreateSmoke);
            smokeEffect.transform.position = holder.transform.position + Vector3.up * 0.5f;
            smokeEffect.transform.localScale = Vector3.one;
            smokeEffect.GetComponent<SmokeZoneBehavior>().Radius = smokeRadius;

            holder.spawnedObjects.Add(smokeEffect);
            Debug.Log($"[SmokeBomb] Deployed at {smokeEffect.transform.position}!");
        }

        public override void OnDeactivate(AbilityHolder holder)
        {
            // Return the cloud to the pool instead of destroying it
            foreach (var smoke in holder.spawnedObjects)
                EffectPool.Release(PoolKey, smoke);
            holder.spawnedObjects.Clear();
        }

        private static GameObject CreateSmoke()
        {
            // Concealment stays distance-based; pooled particles render the cloud separately.
            GameObject smokeEffect = new GameObject("SmokeBomb");
            smokeEffect.AddComponent<SmokeZoneBehavior>();
            return smokeEffect;
        }
    }

    /// <summary>
    /// Active smoke zone registry. Detection code (AI cops, projectile auto-aim)
    /// asks <see cref="IsConcealed"/> whether a position is inside any active cloud.
    /// A distance-based registry (not trigger events) stays correct with pooling —
    /// deactivating a pooled cloud fires no OnTriggerExit, but OnDisable always runs.
    /// </summary>
    public class SmokeZoneBehavior : MonoBehaviour
    {
        private static readonly List<SmokeZoneBehavior> _activeZones = new List<SmokeZoneBehavior>();

        /// <summary>Concealment radius in world units, set on each deployment.</summary>
        public float Radius { get; set; }

        private void OnEnable() => _activeZones.Add(this);

        private void OnDisable() => _activeZones.Remove(this);

        /// <summary>Whether the given world position is hidden inside any active smoke cloud.</summary>
        public static bool IsConcealed(Vector3 position)
        {
            for (int i = 0; i < _activeZones.Count; i++)
            {
                SmokeZoneBehavior zone = _activeZones[i];
                if (zone == null) continue;

                float radius = zone.Radius;
                if ((zone.transform.position - position).sqrMagnitude <= radius * radius)
                    return true;
            }
            return false;
        }
    }
}
