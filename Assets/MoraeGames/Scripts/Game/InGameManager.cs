using UnityEngine;
using UnityEngine.AI;
using MoraeGames.Core;
using MoraeGames.Player;
using MoraeGames.UI;
using MoraeGames.Skill;
using MoraeGames.Network;
using Unity.Netcode;
using System.Collections.Generic;

namespace MoraeGames.Game
{
    /// <summary>
    /// Manages the in-game flow: spawning, ready countdown, timer, and win conditions.
    /// </summary>
    public class InGameManager : MonoBehaviour
    {
        [Header("References")]
        public CameraFollow mainCameraFollow;

        [Header("Prefabs")]
        public GameObject[] copPrefabs;
        public GameObject[] robberPrefabs;

        [Header("Spawn Points")]
        public Transform[] copSpawns;
        public Transform[] robberSpawns;

        [Header("Prison")]
        [Tooltip("Assign the PrisonSpawn transform here for PrisonHelper injection.")]
        public Transform prisonSpawnPoint;

        [Header("Game Rules")]
        public float gameTime = GameConstants.GameTime;
        private float _currentTime;
        private bool _isGameOver = false;
        private bool _isPlaying = false;

        // Ready countdown
        private float _readyCountdown;
        private bool _isReady = false;

        // Cached references (Rule §4: no Find in Update)
        private LootManager _lootManager;
        private List<RobberController> _cachedRobbers = new List<RobberController>();

        // The local player's actual role this match (RoleManager may have been None → randomized)
        private RoleManager.PlayerRole _localRole = RoleManager.PlayerRole.None;

        /// <summary>
        /// How pausing behaves. FreezeTime (timeScale 0) is only valid for LOCAL
        /// matches — a networked simulation keeps running on the server, so freezing
        /// locally desyncs and snaps on resume. When the network layer lands, switch
        /// networked matches to MenuOnly: the pause menu becomes an overlay + local
        /// input block (IsPaused guard) while the world keeps simulating.
        /// </summary>
        public enum PauseMode
        {
            FreezeTime, // single-player: stop the whole simulation
            MenuOnly    // multiplayer-safe: overlay + input block only
        }

        [Header("Pause")]
        public PauseMode pauseMode = PauseMode.FreezeTime;

        /// <summary>
        /// Whether the pause menu is open. Static so input paths can check it cheaply.
        /// In BOTH modes this blocks local gameplay input (Update still runs at
        /// timeScale 0, and in MenuOnly the world keeps running) — "no input while in
        /// menu" is also the correct semantic once this becomes a networked client.
        /// </summary>
        public static bool IsPaused { get; private set; }

        /// <summary>Whether the match has ended (result screen showing).</summary>
        public bool IsGameOver => _isGameOver;

        /// <summary>Opens/closes the pause state. Ignored after game over.</summary>
        public void SetPaused(bool paused)
        {
            if (_isGameOver && paused) return;
            if (IsPaused == paused) return;

            IsPaused = paused;

            // Only a purely local simulation may touch timeScale
            if (pauseMode == PauseMode.FreezeTime)
                Time.timeScale = paused ? 0f : 1f;

            // Drop any buffered touch presses so they don't fire on resume
            if (paused) MobileInput.ResetAll();

            Debug.Log($"[InGameManager] Game {(paused ? "paused" : "resumed")} ({pauseMode}).");
        }

        private void Start()
        {
            _currentTime = gameTime;

            // Inject prison spawn point (Rule §4: avoid GameObject.Find)
            if (prisonSpawnPoint != null)
            {
                PrisonHelper.SetPrisonSpawn(prisonSpawnPoint);
            }
            else
            {
                // Fallback: use the PrisonZone's own position so arrested robbers
                // always land inside the actual rescue trigger, not a stale constant
                PrisonZone fallbackZone = FindFirstObjectByType<PrisonZone>();
                if (fallbackZone != null) PrisonHelper.SetPrisonSpawn(fallbackZone.transform);
            }

            if (NetworkSessionManager.IsNetworked)
            {
                // A networked simulation must never freeze timeScale locally —
                // pause becomes a menu overlay + input block (see PauseMode docs)
                pauseMode = PauseMode.MenuOnly;

                // Only the server spawns; characters replicate to clients, and each
                // machine self-configures via NetworkCharacter.OnNetworkSpawn
                if (NetworkSessionManager.HasStateAuthority)
                    SpawnNetworkedPlayers();
            }
            else
            {
                SpawnPlayersAndBots();
            }

            // Cache scene-level managers (Rule §4: no Find in Update)
            _lootManager = FindFirstObjectByType<LootManager>();

            // PrisonZone.Start may have run before robbers were spawned — refresh its cache
            PrisonZone prisonZone = FindFirstObjectByType<PrisonZone>();
            if (prisonZone != null) prisonZone.RefreshRobberCache();

            // Start with Ready countdown
            _readyCountdown = GameConstants.ReadyCountdownDuration;
            _isReady = true;
            if (GameManager.Instance != null)
                GameManager.Instance.ChangeState(GameManager.GameState.Ready);

            // Freeze all players during countdown
            SetAllPlayersFrozen(true);
        }

        private void Update()
        {
            if (_isGameOver) return;

            // Ready countdown phase
            if (_isReady)
            {
                // Networked clients follow the host's countdown so unfreeze timing
                // matches despite different scene-load moments
                if (!NetworkSessionManager.HasStateAuthority)
                {
                    var sync = NetworkMatchSync.Instance;
                    if (sync == null || !sync.IsSpawned) return;
                    _readyCountdown = sync.ReadyCountdown;
                    if (!sync.IsPlaying) return;
                    _readyCountdown = 0f;
                }
                else
                    _readyCountdown -= Time.deltaTime;

                if (_readyCountdown <= 0f)
                {
                    _isReady = false;
                    _isPlaying = true;
                    SetAllPlayersFrozen(false);
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.ChangeState(GameManager.GameState.Playing);
                        // "GO!" reaches clients via the host's system-message relay
                        if (NetworkSessionManager.HasStateAuthority)
                            GameManager.Instance.BroadcastSystemMessage("GO!", Color.green);
                    }
                }
                return;
            }

            if (!_isPlaying) return;

            // Timer and win evaluation belong to the state authority; clients
            // display the replicated time and receive the result via ClientRpc
            if (NetworkSessionManager.HasStateAuthority)
            {
                UpdateTimer();
                CheckWinConditions();
            }
        }

        // Components disabled by the freeze — unfreeze re-enables ONLY these, never
        // components something else disabled (e.g. NetworkCharacter's remote proxies)
        private readonly List<Behaviour> _frozenComponents = new List<Behaviour>();
        private readonly List<NavMeshAgent> _frozenAgents = new List<NavMeshAgent>();

        private void SetAllPlayersFrozen(bool frozen)
        {
            if (frozen)
            {
                foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
                {
                    if (!agent.enabled || !agent.isOnNavMesh || agent.isStopped) continue;
                    agent.isStopped = true;
                    _frozenAgents.Add(agent);
                }
                foreach (var body in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                    if (!body.isKinematic) body.linearVelocity = Vector3.zero;
                _frozenComponents.Clear();
                var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                foreach (var pc in players)
                {
                    if (!pc.enabled) continue;
                    pc.enabled = false;
                    _frozenComponents.Add(pc);
                }
                var ais = FindObjectsByType<AIController>(FindObjectsSortMode.None);
                foreach (var ai in ais)
                {
                    if (!ai.enabled) continue;
                    ai.enabled = false;
                    _frozenComponents.Add(ai);
                }
            }
            else
            {
                foreach (var agent in _frozenAgents)
                    if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
                _frozenAgents.Clear();
                foreach (var component in _frozenComponents)
                {
                    if (component != null) component.enabled = true;
                }
                _frozenComponents.Clear();
            }
        }

        /// <summary>Returns the ready countdown remaining time (never negative).</summary>
        public float GetReadyCountdown() => Mathf.Max(0f, _readyCountdown);

        /// <summary>Whether the game is in the ready countdown phase.</summary>
        public bool IsReadyPhase => _isReady;

        private void UpdateTimer()
        {
            _currentTime -= Time.deltaTime;
            if (_currentTime <= 0)
            {
                _currentTime = 0;
                EndGame(RoleManager.PlayerRole.Robber, "Time is up.");
            }
        }

        private void CheckWinConditions()
        {
            // Win evaluation belongs to the state authority; clients receive the
            // result via P3 sync (until then their local timer mirrors the host's)
            if (!NetworkSessionManager.HasStateAuthority) return;

            // Count only live robbers: destroyed entries must not block the cop win
            int total = 0;
            int arrestedCount = 0;
            foreach (var robber in _cachedRobbers)
            {
                if (robber == null) continue;
                total++;
                if (robber.IsArrested) arrestedCount++;
            }

            if (total > 0 && arrestedCount >= total)
            {
                Debug.Log($"All {total} robbers arrested! Triggering Cop Win.");
                EndGame(RoleManager.PlayerRole.Cop, "All robbers arrested.");
                return;
            }

            // Robbers win if they steal enough loot (independent of robber count)
            if (_lootManager != null && _lootManager.IsLootWinConditionMet())
            {
                Debug.Log($"Robbers stole {_lootManager.GetStolenPercentage():P0} of loot! Triggering Robber Win.");
                EndGame(RoleManager.PlayerRole.Robber, "Enough loot stolen!");
            }
        }

        /// <summary>
        /// Ends the game, freezes all players, and displays the result UI.
        /// </summary>
        public void EndGame(RoleManager.PlayerRole winner, string reason = null)
        {
            if (_isGameOver) return;

            // In MenuOnly mode the match can end while the pause menu is open —
            // force-close pause before locking the game-over state
            SetPaused(false);

            _isGameOver = true;
            _isPlaying = false;

            // Freeze everyone so no arrests/interactions happen after the result
            SetAllPlayersFrozen(true);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.NotifyGameEnd(winner.ToString());
                string msg = winner == RoleManager.PlayerRole.Cop ? "경찰 승리!" : "도둑 승리!";
                Color color = winner == RoleManager.PlayerRole.Cop ? Color.cyan : Color.red;
                GameManager.Instance.BroadcastSystemMessage(msg, color);
            }

            Debug.Log($"Game Over! Winner: {winner} ({reason})");

            // Broadcast the authoritative result to clients
            if (NetworkSessionManager.IsNetworked && NetworkMatchSync.Instance != null)
            {
                NetworkMatchSync.Instance.BroadcastGameEnd(
                    winner == RoleManager.PlayerRole.Cop, reason ?? "");
            }

            // Award skill points based on whether the local player's team won
            if (SkillTreeManager.Instance != null)
            {
                SkillTreeManager.Instance.AwardMatchPoints(_localRole == winner);
            }

            InGameUIManager uiManager = FindFirstObjectByType<InGameUIManager>();
            if (uiManager != null)
            {
                uiManager.ShowResult(winner, reason);
            }
        }

        /// <summary>Returns the remaining game time in seconds (replicated on clients).</summary>
        public float GetRemainingTime()
        {
            if (!NetworkSessionManager.HasStateAuthority && NetworkMatchSync.Instance != null)
                return NetworkMatchSync.Instance.RemainingTime;
            return _currentTime;
        }

        /// <summary>
        /// Networked clients: applies the host's authoritative match result —
        /// the presentation half of EndGame without re-running any judgment.
        /// </summary>
        public void ApplyRemoteGameEnd(bool copsWin, string reason)
        {
            if (_isGameOver) return;

            SetPaused(false);
            _isGameOver = true;
            _isPlaying = false;
            SetAllPlayersFrozen(true);

            RoleManager.PlayerRole winner = copsWin ? RoleManager.PlayerRole.Cop : RoleManager.PlayerRole.Robber;
            if (GameManager.Instance != null) GameManager.Instance.NotifyGameEnd(winner.ToString());

            // Local team = which controller my owned character carries
            bool foundLocal = false;
            bool localIsCop = false;
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var pc in players)
            {
                if (!pc.isLocalPlayer) continue;
                foundLocal = true;
                localIsCop = pc is CopController;
                break;
            }

            if (foundLocal && SkillTreeManager.Instance != null)
                SkillTreeManager.Instance.AwardMatchPoints(localIsCop == copsWin);

            Debug.Log($"Game Over (remote)! Winner: {winner} ({reason})");

            InGameUIManager uiManager = FindFirstObjectByType<InGameUIManager>();
            if (uiManager != null)
                uiManager.ShowResult(winner, string.IsNullOrEmpty(reason) ? null : reason);
        }

        private void SpawnPlayersAndBots()
        {
            // Misconfigured inspector arrays would throw DivideByZero/IndexOutOfRange below
            if (copPrefabs == null || copPrefabs.Length == 0 || robberPrefabs == null || robberPrefabs.Length == 0
                || copSpawns == null || copSpawns.Length == 0 || robberSpawns == null || robberSpawns.Length == 0)
            {
                Debug.LogError("[InGameManager] Prefab/spawn arrays are not assigned in the inspector. Aborting spawn.");
                return;
            }

            _cachedRobbers.Clear();
            // Direct scene launch and old role selections must follow the same robber-only mode.
            RoleManager.PlayerRole localRole = RoleManager.PlayerRole.Robber;

            _localRole = localRole;
            bool isLocalCop = localRole == RoleManager.PlayerRole.Cop;

            // Get selected character index from RoleManager
            int selectedIndex = 0;
            if (RoleManager.Instance != null)
                selectedIndex = RoleManager.Instance.GetSelectedCharacterIndex("Player1");

            // Spawn cops
            for (int i = 0; i < GameConstants.CopCount; i++)
            {
                GameObject prefab;
                if (isLocalCop && i == 0)
                {
                    int idx = Mathf.Clamp(selectedIndex, 0, copPrefabs.Length - 1);
                    prefab = copPrefabs[idx];
                }
                else
                {
                    prefab = copPrefabs[i % copPrefabs.Length];
                }

                Transform spawn = copSpawns[i % copSpawns.Length];
                GameObject copInstance = Instantiate(prefab, spawn.position, spawn.rotation);

                if (isLocalCop && i == 0) SetupLocalPlayer(copInstance);
                else SetupPlayer(copInstance, false, 0);
            }

            // Spawn robbers
            for (int i = 0; i < GameConstants.RobberCount; i++)
            {
                GameObject prefab;
                if (!isLocalCop && i == 0)
                {
                    int idx = Mathf.Clamp(selectedIndex, 0, robberPrefabs.Length - 1);
                    prefab = robberPrefabs[idx];
                }
                else
                {
                    prefab = robberPrefabs[i % robberPrefabs.Length];
                }

                Transform spawn = robberSpawns[i % robberSpawns.Length];
                GameObject robberInstance = Instantiate(prefab, spawn.position, spawn.rotation);

                // Cache the RobberController for win condition checks
                RobberController rc = robberInstance.GetComponent<RobberController>();
                if (rc != null) _cachedRobbers.Add(rc);

                if (!isLocalCop && i == 0) SetupLocalPlayer(robberInstance);
                else SetupPlayer(robberInstance, false, 0);
            }
        }

        /// <summary>
        /// Server-only: spawns one character per connected client plus AI bots to
        /// fill the 3v5 roster after applying each human's server-confirmed selection. Per-machine configuration
        /// (owner input/camera vs proxy) happens in NetworkCharacter.OnNetworkSpawn.
        /// </summary>
        private void SpawnNetworkedPlayers()
        {
            if (copPrefabs == null || copPrefabs.Length == 0 || robberPrefabs == null || robberPrefabs.Length == 0
                || copSpawns == null || copSpawns.Length == 0 || robberSpawns == null || robberSpawns.Length == 0)
            {
                Debug.LogError("[InGameManager] Prefab/spawn arrays are not assigned. Aborting networked spawn.");
                return;
            }

            _cachedRobbers.Clear();

            int copIndex = 0, robberIndex = 0;
            var room = NetworkRoomState.Instance;
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (room == null || !room.Selections.TryGetValue(clientId, out var selection))
                {
                    Debug.LogError("[InGameManager] Missing confirmed online selection.");
                    return;
                }
                if (clientId == NetworkManager.Singleton.LocalClientId) _localRole = RoleManager.PlayerRole.Robber;
                SpawnNetworkedCharacter(RoleManager.PlayerRole.Robber, clientId, false, ref copIndex, ref robberIndex, Mathf.Clamp(selection.Character,0,robberPrefabs.Length-1));
            }
            while (copIndex < GameConstants.CopCount)
                SpawnNetworkedCharacter(RoleManager.PlayerRole.Cop, 0, true, ref copIndex, ref robberIndex);
            while (robberIndex < GameConstants.RobberCount)
                SpawnNetworkedCharacter(RoleManager.PlayerRole.Robber, 0, true, ref copIndex, ref robberIndex);
            Debug.Log($"[InGameManager] Networked spawn complete: {NetworkManager.Singleton.ConnectedClientsIds.Count} human(s), {copIndex + robberIndex - NetworkManager.Singleton.ConnectedClientsIds.Count} bot(s).");
        }

        private void SpawnNetworkedCharacter(RoleManager.PlayerRole role, ulong ownerClientId, bool isAi,
            ref int copIndex, ref int robberIndex, int selectedCharacter = -1)
        {
            bool isCop = role == RoleManager.PlayerRole.Cop;
            GameObject[] prefabs = isCop ? copPrefabs : robberPrefabs;
            Transform[] spawns = isCop ? copSpawns : robberSpawns;
            int index = isCop ? copIndex++ : robberIndex++;

            GameObject prefab = prefabs[selectedCharacter >= 0 ? selectedCharacter : index % prefabs.Length];
            Transform spawn = spawns[index % spawns.Length];
            GameObject character = Instantiate(prefab, spawn.position, spawn.rotation);

            // Server-side robber cache drives the authoritative win check
            RobberController rc = character.GetComponent<RobberController>();
            if (rc != null) _cachedRobbers.Add(rc);

            NetworkCharacter netCharacter = character.GetComponent<NetworkCharacter>();
            if (netCharacter != null) netCharacter.SetAiControlled(isAi);

            NetworkObject netObj = character.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError($"[InGameManager] {prefab.name} has no NetworkObject — regenerate character prefabs.");
                Destroy(character);
                return;
            }

            if (isAi) netObj.Spawn(true);
            else netObj.SpawnAsPlayerObject(ownerClientId, true);
        }

        private void SetupLocalPlayer(GameObject playerInstance)
        {
            SetupPlayer(playerInstance, true, 0);
        }

        private void SetupPlayer(GameObject character, bool isLocal, int viewId)
        {
            PlayerController pc = character.GetComponent<PlayerController>();
            if (pc != null) pc.isLocalPlayer = isLocal;

            // Dispatch by component, not by prefab name (names are not a contract)
            var aiCop = character.GetComponent<AICopController>();
            if (aiCop != null) aiCop.IsLocalPlayer = isLocal;
            var aiRobber = character.GetComponent<AIRobberController>();
            if (aiRobber != null) aiRobber.IsLocalPlayer = isLocal;

            if (isLocal)
            {
                character.name += "_LocalPlayer";
                AIController aiComponent = character.GetComponent<AIController>();
                if (aiComponent != null) Destroy(aiComponent);

                NavMeshAgent agent = character.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;

                Rigidbody localRb = character.GetComponent<Rigidbody>();
                if (localRb != null)
                {
                    localRb.isKinematic = false;
                    localRb.useGravity = false;
                    localRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    localRb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
                }

                if (mainCameraFollow != null)
                {
                    mainCameraFollow.target = character.transform;
                }

                // Apply unlocked skills to local player
                if (SkillTreeManager.Instance != null)
                {
                    SkillTreeManager.Instance.ApplySkills(pc);
                }
            }
        }

        private void OnDestroy()
        {
            // Never leak a frozen timeScale into the next scene (e.g. exit while paused)
            IsPaused = false;
            Time.timeScale = 1f;

            PrisonHelper.ClearCache();
        }
    }
}

