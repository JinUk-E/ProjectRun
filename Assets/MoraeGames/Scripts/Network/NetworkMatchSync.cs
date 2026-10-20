using UnityEngine;
using Unity.Netcode;
using MoraeGames.Core;
using MoraeGames.Game;

namespace MoraeGames.Network
{
    /// <summary>
    /// Scene network object (placed in InGame by the setup menu) that broadcasts
    /// match-level state from the host: ready countdown, remaining time, system
    /// messages, and the game-end result. Inert in offline play (never spawned).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkMatchSync : NetworkBehaviour
    {
        public static NetworkMatchSync Instance { get; private set; }

        // Server-written, throttled; clients read for display and unfreeze timing
        private readonly NetworkVariable<float> _readyCountdown = new NetworkVariable<float>(0f);
        private readonly NetworkVariable<float> _remainingTime = new NetworkVariable<float>(0f);
        private readonly NetworkVariable<float> _lootPercent = new NetworkVariable<float>(0f);
        private readonly NetworkVariable<bool> _playing = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<bool> _ended = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<bool> _copsWin = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<Unity.Collections.FixedString128Bytes> _reason =
            new NetworkVariable<Unity.Collections.FixedString128Bytes>();
        public bool IsPlaying => IsSpawned && _playing.Value;

        private LootSpot[] _spots;
        private PrisonZone[] _prisons;
        private float _snapshotTimer;
        private bool[] _looted;
        private bool[] _robbing;
        private float[] _progress;
        private float[] _alarms;
        private bool[] _rescuing;
        private float[] _rescueProgress;

        public float ReadyCountdown => _readyCountdown.Value;
        public float RemainingTime => _remainingTime.Value;
        public float LootPercent => _lootPercent.Value;

        private InGameManager _inGameManager;
        private LootManager _lootManager;
        private GameManager _gameManager;

        private void Awake()
        {
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            _gameManager = GameManager.Instance;
            if (_gameManager != null) _gameManager.OnSystemMessage += OnServerSystemMessage;
        }

        public override void OnNetworkDespawn()
        {
            if (_gameManager != null) _gameManager.OnSystemMessage -= OnServerSystemMessage;
            _gameManager = null;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (_inGameManager == null)
            {
                _inGameManager = FindFirstObjectByType<InGameManager>();
                if (_inGameManager == null) return;
            }

            if (!IsServer)
            {
                if (_ended.Value && !_inGameManager.IsGameOver)
                    _inGameManager.ApplyRemoteGameEnd(_copsWin.Value, _reason.Value.ToString());
                return;
            }
            _playing.Value = !_inGameManager.IsReadyPhase && !_inGameManager.IsGameOver;

            // Throttled writes: replicate only meaningful changes, not every frame
            float ready = _inGameManager.GetReadyCountdown();
            if (ShouldReplicateTimer(_readyCountdown.Value, ready, 0.2f))
                _readyCountdown.Value = ready;

            float remaining = _inGameManager.GetRemainingTime();
            if (ShouldReplicateTimer(_remainingTime.Value, remaining, 0.5f))
                _remainingTime.Value = remaining;

            if (_lootManager == null)
                _lootManager = FindFirstObjectByType<LootManager>();
            if (_lootManager != null)
            {
                float loot = _lootManager.GetStolenPercentage();
                if (Mathf.Abs(_lootPercent.Value - loot) > 0.005f)
                    _lootPercent.Value = loot;
            }
            _snapshotTimer -= Time.deltaTime;
            if (_snapshotTimer <= 0f)
            {
                _snapshotTimer = 0.1f;
                CacheTargets();
                for (int i = 0; i < _spots.Length; i++)
                {
                    _looted[i] = _spots[i].IsLooted;
                    _robbing[i] = _spots[i].IsBeingRobbed;
                    _progress[i] = _spots[i].GetProgress();
                    _alarms[i] = _spots[i].building != null ? _spots[i].building.AlarmRemaining : 0;
                }
                for (int i = 0; i < _prisons.Length; i++)
                {
                    _rescuing[i] = _prisons[i].IsBeingRescued;
                    _rescueProgress[i] = _prisons[i].GetRescueProgress();
                }
                InteractionSnapshotClientRpc(_looted, _robbing, _progress, _alarms, _rescuing, _rescueProgress);
            }
        }

        public static bool ShouldReplicateTimer(float previous, float current, float threshold) =>
            Mathf.Abs(previous - current) > threshold || (current <= 0f && previous != 0f);

        private static string SceneKey(Component component)
        {
            string key = "";
            for (Transform t = component.transform; t != null; t = t.parent)
                key = t.GetSiblingIndex().ToString("D6") + "/" + key;
            return key;
        }

        private void CacheTargets()
        {
            if (_spots != null) return;
            _spots = FindObjectsByType<LootSpot>(FindObjectsSortMode.None);
            _prisons = FindObjectsByType<PrisonZone>(FindObjectsSortMode.None);
            System.Array.Sort(_spots, (a, b) => string.CompareOrdinal(SceneKey(a), SceneKey(b)));
            System.Array.Sort(_prisons, (a, b) => string.CompareOrdinal(SceneKey(a), SceneKey(b)));
            _looted = new bool[_spots.Length];
            _robbing = new bool[_spots.Length];
            _progress = new float[_spots.Length];
            _alarms = new float[_spots.Length];
            _rescuing = new bool[_prisons.Length];
            _rescueProgress = new float[_prisons.Length];
        }

        [ClientRpc]
        private void InteractionSnapshotClientRpc(bool[] looted, bool[] robbing, float[] progress, float[] alarms,
            bool[] rescuing, float[] rescueProgress)
        {
            if (IsServer) return;
            CacheTargets();
            if (looted.Length != _spots.Length || rescuing.Length != _prisons.Length) return;
            for (int i = 0; i < _spots.Length; i++)
            {
                _spots[i].ApplyRemoteState(looted[i], robbing[i], progress[i]);
                if (_spots[i].building != null) _spots[i].building.ApplyRemoteAlarm(alarms[i]);
            }
            for (int i = 0; i < _prisons.Length; i++)
                _prisons[i].ApplyRemoteState(rescuing[i], rescueProgress[i]);
        }

        /// <summary>Server-side: relays host system messages to all clients.</summary>
        private void OnServerSystemMessage(string message, Color color)
        {
            SystemMessageClientRpc(message, color);
        }

        [ClientRpc]
        private void SystemMessageClientRpc(string message, Color color)
        {
            // The host already displayed it locally when it raised the event
            if (IsServer) return;
            if (GameManager.Instance != null)
                GameManager.Instance.BroadcastSystemMessage(message, color);
        }

        /// <summary>Server-side: announces the authoritative match result.</summary>
        public void BroadcastGameEnd(bool copsWin, string reason)
        {
            if (!IsSpawned || !IsServer) return;
            _copsWin.Value = copsWin;
            _reason.Value = new Unity.Collections.FixedString128Bytes(reason);
            _playing.Value = false;
            _ended.Value = true;
            GameEndClientRpc(copsWin, reason);
        }

        [ClientRpc]
        private void GameEndClientRpc(bool copsWin, string reason)
        {
            if (IsServer) return; // host already ran EndGame locally

            InGameManager manager = _inGameManager != null ? _inGameManager : FindFirstObjectByType<InGameManager>();
            if (manager != null)
                manager.ApplyRemoteGameEnd(copsWin, reason);
        }
    }
}
