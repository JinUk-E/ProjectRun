using UnityEngine;
using MoraeGames.Player;

namespace MoraeGames.Game
{
    /// <summary>
    /// Exclusive passage that only allows robbers to pass through.
    /// Cops are blocked by the solid collider; robbers get per-collider
    /// collision exemption on first contact.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class VentPassage : MonoBehaviour
    {
        private Collider passageCollider;
        private static readonly System.Collections.Generic.HashSet<Collider> Passages = new System.Collections.Generic.HashSet<Collider>();
        public static bool IsPassage(Collider collider) => Passages.Contains(collider);

        private void Awake()
        {
            passageCollider = GetComponent<Collider>();
            Passages.Add(passageCollider);
        }
        private void OnDestroy() => Passages.Remove(passageCollider);

        private void OnCollisionEnter(Collision collision)
        {
            Transform root = collision.transform.root;

            // Component-based check — tags are not a reliable role contract
            if (root.GetComponent<RobberController>() == null) return;

            // Characters are compound colliders (trigger + solid): exempt all of them,
            // otherwise the robber still bounces off with the non-exempted collider
            foreach (var col in root.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(passageCollider, col, true);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // For trigger-based setup: push back cops (affects non-kinematic bodies,
            // i.e. the locally-controlled cop; AI cops are blocked by the solid collider path)
            Transform root = other.transform.root;
            if (root.GetComponent<CopController>() == null) return;

            Rigidbody rb = root.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                Vector3 pushDir = (root.position - transform.position).normalized;
                rb.AddForce(pushDir * 10f, ForceMode.Impulse);
            }
        }
    }
}
