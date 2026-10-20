using UnityEngine;
using MoraeGames.Core;

namespace MoraeGames.Game
{
    /// <summary>
    /// Interactive loot point that robbers can steal from by holding the interact key.
    /// </summary>
    public class LootSpot : MonoBehaviour
    {
        [Header("Loot Settings")]
        public int lootValue = GameConstants.LootValuePerSpot;
        public float robberyDuration = GameConstants.LootRobberyDuration;
        public HeistBuilding building;

        public bool IsLooted { get; private set; } = false;

        private float _currentProgress = 0f;
        private bool _isBeingRobbed = false;
        private GameObject _currentRobber;

        // Cached references (Rule §4: no Find in Update)
        private LootManager _lootManager;

        // Visual indicator
        private Renderer _indicatorRenderer;
        private Color _originalColor;

        private void Awake()
        {
            // Repair legacy serialized zeros before LootManager totals the scene in Start.
            if (lootValue <= 0) lootValue = GameConstants.LootValuePerSpot;
            if (robberyDuration <= 0) robberyDuration = GameConstants.LootRobberyDuration;
        }

        private void Start()
        {
            _lootManager = FindFirstObjectByType<LootManager>();

            _indicatorRenderer = GetComponentInChildren<Renderer>();
            _originalColor = RendererTint.GetBaseColor(_indicatorRenderer);
        }

        /// <summary>Attempts to start a robbery on this loot spot.</summary>
        /// <returns>True if robbery started successfully.</returns>
        public bool TryStartRobbery(GameObject robber)
        {
            // Judgments run only on the state authority (offline or host);
            // becomes a ServerRpc request in NGO Phase 3
            if (!MoraeGames.Network.NetworkSessionManager.HasStateAuthority) return false;

            // Stale lock from a destroyed robber: release before checking
            if (_isBeingRobbed && _currentRobber == null)
                CancelRobbery();

            if (IsLooted || _isBeingRobbed) return false;
            if (!CanReach(robber)) return false;

            _isBeingRobbed = true;
            _currentRobber = robber;
            _currentProgress = 0f;
            return true;
        }

        /// <summary>Advances the robbery progress by the given delta time.</summary>
        /// <returns>Progress percentage (0-1), or 1 if completed.</returns>
        public float UpdateRobbery(float deltaTime)
        {
            if (!MoraeGames.Network.NetworkSessionManager.HasStateAuthority) return 0f;
            if (!_isBeingRobbed || IsLooted) return 0f;
            if (!CanReach(_currentRobber)) { CancelRobbery(); return 0f; }
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0) return GetProgress();
            if (building != null) building.RecordTampering(Mathf.Min(deltaTime, robberyDuration - _currentProgress));

            _currentProgress += deltaTime;

            // Update visual feedback (MPB tint — no material instantiation)
            if (_indicatorRenderer != null)
            {
                float t = _currentProgress / robberyDuration;
                RendererTint.SetColor(_indicatorRenderer, Color.Lerp(_originalColor, Color.gray, t));
            }

            if (_currentProgress >= robberyDuration)
            {
                CompleteRobbery();
                return 1f;
            }

            return _currentProgress / robberyDuration;
        }

        /// <summary>Cancels the current robbery and resets progress.</summary>
        public void CancelRobbery()
        {
            _isBeingRobbed = false;
            _currentRobber = null;
            _currentProgress = 0f;

            RendererTint.ClearColor(_indicatorRenderer);
        }

        private void CompleteRobbery()
        {
            IsLooted = true;
            _isBeingRobbed = false;

            RendererTint.SetColor(_indicatorRenderer, Color.gray);

            // Use cached LootManager (Rule §4: no Find at runtime)
            if (_lootManager != null)
            {
                _lootManager.RegisterStolen(lootValue, _currentRobber);
            }

            _currentRobber = null;
            Debug.Log($"[LootSpot] {gameObject.name} has been robbed! Value: {lootValue}");
        }

        /// <summary>Returns the current robbery progress as a 0-1 value.</summary>
        public float GetProgress() => _isBeingRobbed ? _currentProgress / robberyDuration : 0f;

        /// <summary>Whether this spot is currently being robbed.</summary>
        public bool IsBeingRobbed => _isBeingRobbed;

        /// <summary>Shared server-side reachability check for human players and bots.</summary>
        public bool CanReach(GameObject robber)
        {
            if (robber == null) return false;
            var controller = robber.GetComponent<MoraeGames.Player.RobberController>();
            if (controller == null || controller.IsArrested) return false;
            return Vector3.Distance(robber.transform.position, transform.position) <= controller.interactionRange * 1.5f
                && (building == null || building.Contains(robber.transform.position))
                && HeistBuilding.HasSight(robber.transform.position, transform.position);
        }

        /// <summary>Presentation only; never awards loot on clients.</summary>
        public void ApplyRemoteState(bool looted, bool beingRobbed, float progress)
        {
            if (MoraeGames.Network.NetworkSessionManager.HasStateAuthority) return;
            IsLooted = looted;
            _isBeingRobbed = beingRobbed;
            _currentProgress = Mathf.Clamp01(progress) * robberyDuration;
            if (looted) RendererTint.SetColor(_indicatorRenderer, Color.gray);
            else if (beingRobbed) RendererTint.SetColor(_indicatorRenderer, Color.Lerp(_originalColor, Color.gray, Mathf.Clamp01(progress)));
            else RendererTint.ClearColor(_indicatorRenderer);
        }
    }
}
