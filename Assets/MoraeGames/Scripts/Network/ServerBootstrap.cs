using UnityEngine;
using Unity.Netcode;
using MoraeGames.Game;

namespace MoraeGames.Network
{
    /// <summary>
    /// Dedicated-server lifecycle driver. Lives in the MainMenu (boot) scene;
    /// on client builds it destroys itself immediately.
    ///
    /// Flow: auto StartServer on boot → wait for players → begin the match when
    /// full or after a grace period past the minimum count → after the match ends,
    /// quit so the orchestrator (Multiplay / VPS script) can recycle the instance.
    /// </summary>
    public class ServerBootstrap : MonoBehaviour
    {
        [Header("Match Start Rules")]
        [Tooltip("Minimum connected players before the start countdown begins.")]
        public int minPlayersToStart = 2;
        [Tooltip("Grace period (seconds) after reaching the minimum, letting more players join.")]
        public float lobbyWaitAfterMin = 10f;

        [Header("Lifecycle")]
        [Tooltip("Seconds after the match result before the server process exits.")]
        public float shutdownDelayAfterEnd = 15f;

        private float _startTimer = -1f;
        private float _shutdownTimer = -1f;
        private bool _matchBegun;
        private InGameManager _inGameManager;

        private void Start()
        {
            if (!IsDedicatedServer())
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            Debug.Log("[ServerBootstrap] Dedicated server booting...");
            NetworkSessionManager.StartServer();
        }

        private void Update()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            if (!_matchBegun)
            {
                UpdateLobbyPhase(nm);
            }
            else
            {
                UpdateShutdownPhase();
            }
        }

        private void UpdateLobbyPhase(NetworkManager nm)
        {
            int players = nm.ConnectedClientsList.Count;

            if (players < minPlayersToStart)
            {
                _startTimer = -1f;
                return;
            }

            if (_startTimer < 0f)
            {
                _startTimer = lobbyWaitAfterMin;
                Debug.Log($"[ServerBootstrap] {players} player(s) connected — starting in {lobbyWaitAfterMin}s (or when full).");
            }

            _startTimer -= Time.deltaTime;
            bool full = players >= NetworkSessionManager.MaxPlayers;

            if (_startTimer <= 0f || full)
            {
                Debug.Log($"[ServerBootstrap] Beginning match with {players} player(s).");
                NetworkSessionManager.BeginMatch();
                _matchBegun = NetworkSessionManager.MatchStarted;
            }
        }

        private void UpdateShutdownPhase()
        {
            if (_inGameManager == null)
            {
                _inGameManager = FindFirstObjectByType<InGameManager>();
                if (_inGameManager == null) return;
            }

            if (!_inGameManager.IsGameOver) return;

            if (_shutdownTimer < 0f)
            {
                _shutdownTimer = shutdownDelayAfterEnd;
                Debug.Log($"[ServerBootstrap] Match over — shutting down in {shutdownDelayAfterEnd}s.");
            }

            _shutdownTimer -= Time.deltaTime;
            if (_shutdownTimer <= 0f)
            {
                NetworkSessionManager.Shutdown();
                Application.Quit();
            }
        }

        /// <summary>True on Dedicated Server builds, or any headless (-batchmode) run.</summary>
        public static bool IsDedicatedServer()
        {
#if UNITY_SERVER
            return true;
#else
            return Application.isBatchMode;
#endif
        }
    }
}
