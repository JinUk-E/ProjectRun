using System;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace MoraeGames.Network
{
    /// <summary>
    /// Session-mode orchestration over NGO's NetworkManager singleton.
    /// Offline (default) keeps the single-player flow untouched.
    ///
    /// Session flow: hosting (Relay with join code, or direct IP as LAN fallback)
    /// keeps everyone in the Lobby scene while players connect; BeginMatch() then
    /// drives the networked scene change for all peers. Late joins after match
    /// start are rejected at connection approval.
    /// </summary>
    public static class NetworkSessionManager
    {
        public enum SessionMode
        {
            Offline,
            Host,
            Client,
            Server // dedicated: authority without a local player
        }

        public static SessionMode Mode { get; private set; } = SessionMode.Offline;
        public static bool IsNetworked => Mode != SessionMode.Offline;

        /// <summary>
        /// True when this machine may run authoritative game logic: offline play,
        /// or the server/host of a networked session. Judgment code (arrest, rescue,
        /// robbery, win conditions) must check this so clients never fork the state.
        /// </summary>
        public static bool HasStateAuthority =>
            !IsNetworked || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);

        /// <summary>Set when the host loads InGame; blocks late joins.</summary>
        public static bool MatchStarted { get; private set; }

        /// <summary>Relay join code of the current hosted session (null for direct IP).</summary>
        public static string CurrentJoinCode { get; private set; }

        public const int MaxPlayers = 8;
        public const ushort DefaultPort = 7777;

        private static NetworkManager _callbackManager;
        private static int _sessionRevision;

        // ---------------------------------------------------------------- Relay

        /// <summary>
        /// Hosts via Unity Relay and returns the join code (null on failure).
        /// Requires the project to be linked to a Unity Cloud project with Relay
        /// enabled (Project Settings → Services).
        /// </summary>
        public static async Task<string> StartHostWithRelayAsync(CancellationToken cancellationToken = default)
        {
            int revision = ++_sessionRevision;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[NetworkSession] No NetworkManager. Run 'MoraeGames/Setup Network Manager' first.");
                return null;
            }

            try
            {
                await EnsureServicesAsync();
                if (cancellationToken.IsCancellationRequested || revision != _sessionRevision || nm == null) return null;

                // maxConnections excludes the host itself
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
                if (cancellationToken.IsCancellationRequested || revision != _sessionRevision || nm == null) return null;
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                if (cancellationToken.IsCancellationRequested || revision != _sessionRevision || nm == null) return null;

                var transport = nm.GetComponent<UnityTransport>();
                transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

                if (!StartHostInternal(nm)) return null;

                CurrentJoinCode = joinCode;
                Debug.Log($"[NetworkSession] Relay host up. Join code: {joinCode}");
                return joinCode;
            }
            catch (Exception e)
            {
                if (cancellationToken.IsCancellationRequested || revision != _sessionRevision) return null;
                Debug.LogError($"[NetworkSession] Relay host failed: {e.Message}");
                return null;
            }
        }

        /// <summary>Joins a Relay session by join code.</summary>
        public static async Task<bool> StartClientWithRelayAsync(string joinCode, CancellationToken cancellationToken = default)
        {
            int revision = ++_sessionRevision;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[NetworkSession] No NetworkManager. Run 'MoraeGames/Setup Network Manager' first.");
                return false;
            }

            try
            {
                await EnsureServicesAsync();
                if (cancellationToken.IsCancellationRequested || revision != _sessionRevision || nm == null) return false;

                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                if (cancellationToken.IsCancellationRequested || revision != _sessionRevision || nm == null) return false;

                var transport = nm.GetComponent<UnityTransport>();
                transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

                RegisterCallbacks(nm);
                if (!nm.StartClient()) return false;

                Mode = SessionMode.Client;
                NetworkRoomState.Initialize(nm);
                Debug.Log($"[NetworkSession] Joining relay session {joinCode}...");
                return true;
            }
            catch (Exception e)
            {
                if (cancellationToken.IsCancellationRequested || revision != _sessionRevision) return false;
                Debug.LogError($"[NetworkSession] Relay join failed: {e.Message}");
                return false;
            }
        }

        private static async Task EnsureServicesAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // ------------------------------------------------------------ Direct IP

        /// <summary>LAN/dev fallback: hosts on all interfaces without Relay.</summary>
        public static bool StartHost()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[NetworkSession] No NetworkManager. Run 'MoraeGames/Setup Network Manager' first.");
                return false;
            }

            var transport = nm.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData("0.0.0.0", DefaultPort, "0.0.0.0");

            if (!StartHostInternal(nm)) return false;

            CurrentJoinCode = null;
            Debug.Log($"[NetworkSession] Direct host up on port {DefaultPort}.");
            return true;
        }

        /// <summary>LAN/dev fallback: joins a direct-IP host.</summary>
        public static bool StartClient(string address)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[NetworkSession] No NetworkManager. Run 'MoraeGames/Setup Network Manager' first.");
                return false;
            }

            var transport = nm.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData(address, DefaultPort);

            RegisterCallbacks(nm);
            if (!nm.StartClient()) return false;

            Mode = SessionMode.Client;
            NetworkRoomState.Initialize(nm);
            Debug.Log($"[NetworkSession] Connecting to {address}:{DefaultPort}...");
            return true;
        }

        // ------------------------------------------------------------ Lifecycle

        /// <summary>Dedicated server: listens without a local player. Headless entry point.</summary>
        public static bool StartServer()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[NetworkSession] No NetworkManager. Run 'MoraeGames/Setup Network Manager' first.");
                return false;
            }

            var transport = nm.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData("0.0.0.0", DefaultPort, "0.0.0.0");

            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = ApproveConnection;
            RegisterCallbacks(nm);

            if (!nm.StartServer())
            {
                Debug.LogError("[NetworkSession] StartServer failed.");
                return false;
            }

            Mode = SessionMode.Server;
            NetworkRoomState.Initialize(nm);
            MatchStarted = false;
            Debug.Log($"[NetworkSession] Dedicated server listening on port {DefaultPort}.");
            return true;
        }

        /// <summary>Authority-only: starts the match — networked scene change for all peers.</summary>
        public static void BeginMatch()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || MatchStarted) return;
            if (NetworkRoomState.Instance == null || !NetworkRoomState.Instance.CanBegin()) return;

            MatchStarted = nm.SceneManager.LoadScene("InGame", LoadSceneMode.Single) == SceneEventProgressStatus.Started;
            if (MatchStarted) NetworkRoomState.Instance.LoadingStarted();
        }

        internal static void SetMatchStarted(bool value) => MatchStarted = value;

        /// <summary>Host returns the whole party to its lobby without closing the transport or Relay allocation.</summary>
        public static void ReturnToRoom()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || !MatchStarted || NetworkRoomState.Instance == null || NetworkRoomState.Instance.IsLoading) return;
            var match = UnityEngine.Object.FindFirstObjectByType<MoraeGames.Game.InGameManager>();
            if (match == null || !match.IsGameOver) return;
            if (nm.SceneManager.LoadScene("Lobby", LoadSceneMode.Single) == SceneEventProgressStatus.Started)
                NetworkRoomState.Instance.LoadingStarted();
        }

        /// <summary>Ends the current session (both roles) and returns to offline mode.</summary>
        public static void Shutdown()
        {
            ++_sessionRevision;
            NetworkManager nm = NetworkManager.Singleton;
            Mode = SessionMode.Offline;
            NetworkRoomState.Close();
            if (nm != null && (nm.IsHost || nm.IsClient || nm.IsServer))
                nm.Shutdown();

            Mode = SessionMode.Offline;
            MatchStarted = false;
            CurrentJoinCode = null;
        }

        private static bool StartHostInternal(NetworkManager nm)
        {
            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = ApproveConnection;
            RegisterCallbacks(nm);

            if (!nm.StartHost())
            {
                Debug.LogError("[NetworkSession] StartHost failed.");
                return false;
            }

            Mode = SessionMode.Host;
            MatchStarted = false;
            NetworkRoomState.Initialize(nm);
            return true;
        }

        private static void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            NetworkManager nm = NetworkManager.Singleton;
            bool hasRoom = nm != null && nm.ConnectedClientsList.Count < MaxPlayers;

            response.Approved = hasRoom && !MatchStarted;
            // Player objects are spawned explicitly by the match flow (role-based
            // prefab selection), not automatically on connect
            response.CreatePlayerObject = false;
            if (!hasRoom) response.Reason = "Match is full.";
            else if (MatchStarted) response.Reason = "Match already started.";
        }

        private static void RegisterCallbacks(NetworkManager nm)
        {
            if (_callbackManager == nm) return;
            if (_callbackManager != null) _callbackManager.OnClientDisconnectCallback -= OnClientDisconnect;
            _callbackManager = nm;
            nm.OnClientDisconnectCallback += OnClientDisconnect;
        }

        private static void OnClientDisconnect(ulong clientId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;

            // Our own disconnection (kicked, host quit, timeout) → back to the lobby
            if (Mode == SessionMode.Client && clientId == nm.LocalClientId)
            {
                Debug.Log("[NetworkSession] Disconnected from host. Returning to lobby.");
                Mode = SessionMode.Offline;
                MatchStarted = false;
                CurrentJoinCode = null;
                NetworkRoomState.Close();
                SceneManager.LoadScene("Lobby");
            }
            else if (Mode == SessionMode.Host)
            {
                Debug.Log($"[NetworkSession] Client {clientId} disconnected.");
            }
        }
    }
}
