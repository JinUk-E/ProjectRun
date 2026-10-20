using UnityEngine;
using System.Collections.Generic;
using MoraeGames.Core;

namespace MoraeGames.Ability
{
    [CreateAssetMenu(menuName = "Abilities/Cop/AreaScan")]
    public class AreaScanData : AbilityDataSO
    {
        [Header("Area Scan")]
        public float scanRadius = 30f;
        public Color markerColor = new Color(1f, 1f, 0f, 0.7f);
        public float markerSize = 0.8f;

        private const string PoolKey = "ScanMarker";

        public override void OnActivate(AbilityHolder holder)
        {
            holder.savedValues["scanPulse"] = 0;
            Reveal(holder);
        }
        public override void OnActiveUpdate(AbilityHolder holder)
        {
            float time = holder.savedValues["scanPulse"] + Time.deltaTime;
            if (time >= 1.2f) { OnDeactivate(holder); Reveal(holder); time = 0; }
            holder.savedValues["scanPulse"] = time;
        }
        private void Reveal(AbilityHolder holder)
        {
            Collider[] colliders = Physics.OverlapSphere(holder.transform.position, scanRadius);

            // Characters are compound colliders — dedupe per root or each robber gets stacked markers
            var markedRoots = new HashSet<Transform>();
            foreach (var hit in colliders)
            {
                if (hit.transform.root.CompareTag("Robber"))
                {
                    Transform robberRoot = hit.transform.root;
                    if (!markedRoots.Add(robberRoot)) continue;

                    // Pooled marker (Rule §4: pooling mandatory for effects)
                    GameObject marker = EffectPool.Get(PoolKey, CreateMarker);
                    marker.transform.SetParent(robberRoot, false);
                    marker.transform.position = robberRoot.position + Vector3.up * 3f;
                    marker.transform.localScale = Vector3.one * markerSize;
                    RendererTint.SetColor(marker.GetComponent<Renderer>(), markerColor);

                    holder.spawnedObjects.Add(marker);
                }
            }
            Debug.Log($"[AreaScan] Revealed {holder.spawnedObjects.Count} robbers within {scanRadius}m!");
        }

        public override void OnDeactivate(AbilityHolder holder)
        {
            // Return markers to the pool instead of destroying them
            foreach (var marker in holder.spawnedObjects)
                EffectPool.Release(PoolKey, marker);
            holder.spawnedObjects.Clear();
        }

        private static GameObject CreateMarker()
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "ScanMarker";
            marker.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("AbilityRing");
            Object.Destroy(marker.GetComponent<Collider>());
            return marker;
        }
    }
}
