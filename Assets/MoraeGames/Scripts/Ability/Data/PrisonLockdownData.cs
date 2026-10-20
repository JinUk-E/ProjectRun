using UnityEngine;
using MoraeGames.Core;

namespace MoraeGames.Ability
{
    [CreateAssetMenu(menuName = "Abilities/Cop/PrisonLockdown")]
    public class PrisonLockdownData : AbilityDataSO
    {
        [Header("Prison Lockdown")]
        public float lockdownRadius = 7f;
        public Color shieldColor = new Color(0.2f, 0.5f, 1f, 0.3f);

        public override void OnActivate(AbilityHolder holder)
        {
            // Use the scene-injected prison position, not the hardcoded constant
            Vector3 prisonPos = PrisonHelper.GetPrisonPosition();

            GameObject forceField = new GameObject("PrisonLockdown_ForceField");
            forceField.transform.position = prisonPos;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "ForceFieldVisual";
            visual.transform.SetParent(forceField.transform);
            visual.transform.localPosition = Vector3.up * 1f;
            visual.transform.localScale = Vector3.one * lockdownRadius * 2f;

            RendererTint.SetColor(visual.GetComponent<Renderer>(), shieldColor);
            visual.GetComponent<Collider>().isTrigger = false;
            visual.AddComponent<LockdownBlocker>();

            holder.spawnedObjects.Add(forceField);
            Debug.Log($"[PrisonLockdown] Forcefield activated for {duration}s!");
        }

        public override void OnDeactivate(AbilityHolder holder)
        {
            holder.CleanupSpawnedObjects();
        }
    }

    public class LockdownBlocker : MonoBehaviour
    {
        private static readonly System.Collections.Generic.HashSet<Collider> Blockers = new System.Collections.Generic.HashSet<Collider>();
        private Collider _collider;
        public static bool IsBlocker(Collider collider) => Blockers.Contains(collider);
        private void Awake() { _collider = GetComponent<Collider>(); Blockers.Add(_collider); }
        private void OnDestroy() => Blockers.Remove(_collider);
        private void OnCollisionEnter(Collision collision)
        {
            Transform root = collision.transform.root;
            if (root.GetComponent<MoraeGames.Player.CopController>() == null) return;

            // Compound colliders: exempt every collider under the cop's root at once,
            // otherwise the first touch is still a physical bump per collider
            Collider own = GetComponent<Collider>();
            foreach (var col in root.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(own, col, true);
        }
    }
}
