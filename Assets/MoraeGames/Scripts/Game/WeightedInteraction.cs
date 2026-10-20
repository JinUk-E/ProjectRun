using UnityEngine;
using System.Collections.Generic;
using MoraeGames.Player;

namespace MoraeGames.Game
{
    /// <summary>
    /// Interaction where progress speed multiplies based on the number of
    /// overlapping players in the trigger zone.
    /// Occupants are tracked per character root (not a raw counter) so destroyed
    /// objects and multi-collider characters can never corrupt the count.
    /// </summary>
    public class WeightedInteraction : MonoBehaviour
    {
        [Header("Settings")]
        public float totalRequired = 5f; // Seconds at 1x speed to complete
        public bool robberOnly = true;

        private float progress = 0f;
        private bool isCompleted = false;

        // Root transform -> number of its colliders currently inside the zone
        private readonly Dictionary<Transform, int> _occupants = new Dictionary<Transform, int>();
        private static readonly List<Transform> _staleBuffer = new List<Transform>();

        // Visual
        private Renderer indicatorRenderer;
        private Color originalColor;

        public float Progress => progress / totalRequired;
        public bool IsCompleted => isCompleted;
        public int PlayerCount => _occupants.Count;

        private void Start()
        {
            indicatorRenderer = GetComponentInChildren<Renderer>();
            originalColor = MoraeGames.Core.RendererTint.GetBaseColor(indicatorRenderer);
        }

        private void Update()
        {
            if (isCompleted) return;

            // Destroyed occupants never fire OnTriggerExit — prune them here
            if (_occupants.Count > 0)
            {
                _staleBuffer.Clear();
                foreach (var kv in _occupants)
                    if (kv.Key == null) _staleBuffer.Add(kv.Key);
                foreach (var stale in _staleBuffer)
                    _occupants.Remove(stale);
            }

            int playerCount = _occupants.Count;
            if (playerCount <= 0) return;

            // Progress multiplied by number of players
            progress += Time.deltaTime * playerCount;

            // Visual feedback (MPB tint — no material instantiation)
            if (indicatorRenderer != null)
            {
                float t = progress / totalRequired;
                MoraeGames.Core.RendererTint.SetColor(indicatorRenderer, Color.Lerp(originalColor, Color.green, t));
            }

            if (progress >= totalRequired)
            {
                isCompleted = true;
                OnCompleted();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isCompleted) return;
            Transform root = other.transform.root;
            if (!IsEligible(root)) return;

            _occupants.TryGetValue(root, out int count);
            _occupants[root] = count + 1;
        }

        private void OnTriggerExit(Collider other)
        {
            if (isCompleted) return;
            Transform root = other.transform.root;
            if (!_occupants.TryGetValue(root, out int count)) return;

            if (count <= 1) _occupants.Remove(root);
            else _occupants[root] = count - 1;
        }

        // Component-based check: decoys/props have no controller, and a disguised
        // robber keeps counting even if visuals change.
        private bool IsEligible(Transform root)
        {
            bool isRobber = root.GetComponent<RobberController>() != null;
            if (robberOnly) return isRobber;
            return isRobber || root.GetComponent<CopController>() != null;
        }

        protected virtual void OnCompleted()
        {
            Debug.Log($"[WeightedInteraction] {gameObject.name} completed with {PlayerCount} players!");
        }
    }
}
