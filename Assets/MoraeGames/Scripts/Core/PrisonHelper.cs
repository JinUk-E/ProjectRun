using UnityEngine;

namespace MoraeGames.Core
{
    /// <summary>
    /// Provides prison-related utility methods.
    /// Call <see cref="SetPrisonSpawn"/> from scene setup to avoid runtime Find calls.
    /// </summary>
    public static class PrisonHelper
    {
        private static Transform _cachedPrisonSpawn;

        /// <summary>
        /// Explicitly sets the prison spawn point. Must be called during scene initialization
        /// to avoid expensive GameObject.Find at runtime.
        /// </summary>
        public static void SetPrisonSpawn(Transform prisonSpawn)
        {
            _cachedPrisonSpawn = prisonSpawn;
        }

        /// <summary>
        /// Returns the cached prison position, or falls back to the constant default.
        /// </summary>
        public static Vector3 GetPrisonPosition()
        {
            if (_cachedPrisonSpawn != null)
                return _cachedPrisonSpawn.position;

            Debug.LogWarning("[PrisonHelper] Prison spawn not set. Using default position. Call SetPrisonSpawn() during scene setup.");
            return GameConstants.PrisonPosition;
        }

        /// <summary>
        /// Teleports a character to prison, resetting physics state.
        /// </summary>
        public static void TeleportToPrison(Transform target, Rigidbody rb)
        {
            Vector3 jailPos = GetPrisonPosition();

            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                // Continuous CD is invalid on kinematic bodies (Unity logs an error);
                // switch to speculative before flipping kinematic
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.position = jailPos;
            }
            target.position = jailPos;
            var networkTransform = target.GetComponent<MoraeGames.Network.OwnerNetworkTransform>();
            if (networkTransform != null) networkTransform.TeleportOnAuthority(jailPos);
        }

        /// <summary>
        /// Clears the cached prison spawn reference. Call on scene unload.
        /// </summary>
        public static void ClearCache()
        {
            _cachedPrisonSpawn = null;
        }
    }
}
