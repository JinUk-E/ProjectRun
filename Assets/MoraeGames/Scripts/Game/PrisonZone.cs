using UnityEngine;
using MoraeGames.Core;
using MoraeGames.Player;

namespace MoraeGames.Game
{
    /// <summary>
    /// Prison zone where arrested robbers are held. Robbers can rescue teammates
    /// by interacting with this zone.
    /// </summary>
    public class PrisonZone : MonoBehaviour
    {
        [Header("Rescue Settings")]
        public float rescueDuration = GameConstants.RescueDuration;

        private float _rescueProgress = 0f;
        private bool _isBeingRescued = false;
        private GameObject _rescuer;

        // Cached robber list. Robbers are spawned by InGameManager.Start, whose execution
        // order relative to this Start is not guaranteed — so the cache self-heals whenever
        // it is empty (see HasArrestedRobbers), and InGameManager refreshes it after spawning.
        private RobberController[] _allRobbers;

        private void Awake()
        {
            if (rescueDuration <= 0) rescueDuration = GameConstants.RescueDuration;
        }

        private void Start()
        {
            RefreshRobberCache();
        }

        /// <summary>Refreshes the cached list of all robbers in the scene.</summary>
        public void RefreshRobberCache()
        {
            _allRobbers = FindObjectsByType<RobberController>(FindObjectsSortMode.None);
        }

        /// <summary>Checks if any robbers are currently arrested.</summary>
        public bool HasArrestedRobbers()
        {
            if (_allRobbers == null || _allRobbers.Length == 0)
                RefreshRobberCache();

            foreach (var robber in _allRobbers)
            {
                if (robber != null && robber.IsArrested)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Starts a rescue attempt by the given rescuer.
        /// Fails (returns false) if a rescue is already in progress — a second rescuer
        /// must never reset the accumulated progress — or if nobody is arrested.
        /// </summary>
        public bool TryStartRescue(GameObject rescuerObj)
        {
            // Judgments run only on the state authority (offline or host);
            // becomes a ServerRpc request in NGO Phase 3
            if (!MoraeGames.Network.NetworkSessionManager.HasStateAuthority) return false;

            if (_isBeingRescued) return false;
            if (!HasArrestedRobbers()) return false;

            _isBeingRescued = true;
            _rescuer = rescuerObj;
            _rescueProgress = 0f;
            return true;
        }

        /// <summary>Advances rescue progress by the given delta time.</summary>
        public void UpdateRescue(float deltaTime)
        {
            if (!_isBeingRescued) return;

            // Rescuer destroyed mid-rescue: release the lock instead of deadlocking
            if (_rescuer == null)
            {
                CancelRescue();
                return;
            }

            _rescueProgress += deltaTime;

            if (_rescueProgress >= rescueDuration)
            {
                CompleteRescue();
            }
        }

        /// <summary>Cancels the current rescue attempt and resets progress.</summary>
        public void CancelRescue()
        {
            _isBeingRescued = false;
            _rescuer = null;
            _rescueProgress = 0f;
        }

        private void CompleteRescue()
        {
            _isBeingRescued = false;

            // Mass rescue: free ALL arrested robbers at once per game design spec
            if (_allRobbers == null || _allRobbers.Length == 0)
                RefreshRobberCache();

            int rescuedCount = 0;
            foreach (var robber in _allRobbers)
            {
                if (robber == null || !robber.IsArrested) continue;

                // Teleport first so the NavMeshAgent re-enables at the exit position,
                // then flip the state (OnRescued -> HandleRescuedState enables the agent).
                float angle = rescuedCount * Mathf.PI * 2f / 6f;
                float exitDist = GameConstants.PrisonZoneRadius + 2f;
                Vector3 rescueExit = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * exitDist;

                Rigidbody rb = robber.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // AI robbers stay kinematic — their movement is NavMeshAgent-driven
                    var character = robber.GetComponent<MoraeGames.Network.NetworkCharacter>();
                    rb.isKinematic = MoraeGames.Network.NetworkSessionManager.IsNetworked
                        ? character == null || character.IsAiControlled || !MoraeGames.Network.NetworkSessionManager.HasStateAuthority
                        : !robber.isLocalPlayer;
                    // Restore Continuous CD for the local player (dropped to speculative on arrest)
                    if (!rb.isKinematic)
                        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    rb.position = rescueExit;
                }
                robber.transform.position = rescueExit;
                var networkTransform = robber.GetComponent<MoraeGames.Network.OwnerNetworkTransform>();
                if (networkTransform != null) networkTransform.TeleportOnAuthority(rescueExit);

                robber.OnRescued();
                rescuedCount++;
            }

            Debug.Log($"[PrisonZone] Mass rescue! {rescuedCount} robbers freed by {_rescuer?.name}!");

            if (rescuedCount > 0 && GameManager.Instance != null)
                GameManager.Instance.BroadcastSystemMessage($"구출 성공! 도둑 {rescuedCount}명이 탈출했어요", Color.green);

            _rescuer = null;
            _rescueProgress = 0f;
        }

        /// <summary>Returns the current rescue progress as a 0-1 value.</summary>
        public float GetRescueProgress() => _isBeingRescued ? _rescueProgress / rescueDuration : 0f;

        /// <summary>Whether this zone is currently being rescued.</summary>
        public bool IsBeingRescued => _isBeingRescued;

        /// <summary>Copies server progress without running rescue gameplay.</summary>
        public void ApplyRemoteState(bool rescuing, float progress)
        {
            if (MoraeGames.Network.NetworkSessionManager.HasStateAuthority) return;
            _isBeingRescued = rescuing;
            _rescueProgress = Mathf.Clamp01(progress) * rescueDuration;
        }
    }
}
