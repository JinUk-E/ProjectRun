using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
using MoraeGames.Network;
using MoraeGames.Core;
using MoraeGames.Game;
using MoraeGames.Player;
using MoraeGames.Ability;

namespace MoraeGames.Editor
{
    /// <summary>Batch Play Mode integration check; does not award or write player progression.</summary>
    public static class ReleaseNetworkSmoke
    {
        private const string Key = "MoraeGames.ReleaseSmoke";
        private static double _deadline;
        private static int _stage;
        private static string _failure;
        private static NetworkCharacter _human;
        private static Vector3 _movementStart;
        private static double _movementTime;

        public static void Run()
        {
            ReleaseReadinessRepair.Run();
            EditorSceneManager.OpenScene("Assets/MoraeGames/Scenes/MainMenu.unity");
            // Prevent auto-dedicated startup for this host smoke test; never save this override.
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<ServerBootstrap>();
            if (bootstrap != null) bootstrap.enabled = false;
            SessionState.SetBool(Key, true);
            Install();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (SessionState.GetBool(Key, false)) Install();
        }

        private static void Install()
        {
            _deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                _failure = message;
        }

        private static void Tick()
        {
            try
            {
                if (_failure != null) throw new InvalidOperationException(_failure);
                if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("Network smoke timed out at stage " + _stage);
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                var nm = NetworkManager.Singleton;
                if (_stage == 0)
                {
                    if (nm == null) return;
                    Check(NetworkSessionManager.StartHost(), "Host startup");
                    NetworkSessionManager.BeginMatch();
                    Check(NetworkSessionManager.MatchStarted, "Network scene load starts");
                    _stage = 1;
                }
                else if (_stage == 1)
                {
                    var match = UnityEngine.Object.FindFirstObjectByType<InGameManager>();
                    if (match == null || !match.IsReadyPhase) return;
                    var characters = UnityEngine.Object.FindObjectsByType<NetworkCharacter>(FindObjectsSortMode.None);
                    if (characters.Length != 8) return;
                    Check(characters.All(c => c.IsSpawned), "All eight characters spawned");
                    Check(characters.Count(c => c.IsAiControlled) == 7, "One human and seven bots");
                    Check(NetworkMatchSync.Instance != null && NetworkMatchSync.Instance.IsSpawned, "Scene state synchronizer spawned");
                    var human = characters.Single(c => !c.IsAiControlled);
                    Check(!human.RequestAbility(), "Reject ability during ready phase");
                    _stage = 2;
                }
                else if (_stage == 2)
                {
                    if (!NetworkCharacter.MatchAllowsActions || !NetworkMatchSync.Instance.IsPlaying) return;
                    var characters = UnityEngine.Object.FindObjectsByType<NetworkCharacter>(FindObjectsSortMode.None);
                    var human = characters.Single(c => !c.IsAiControlled);
                    Check(human.GetComponent<PlayerController>().enabled, "Human unfreezes after countdown");
                    Check(!human.GetComponent<Rigidbody>().isKinematic, "Server owns human physics");
                    Check(human.RequestAbility(), "Server accepts ready ability during play");
                    Check(!human.RequestAbility(), "Server rejects duplicate ability during cooldown");
                    _human = human;
                    // Stop local keyboard polling so the test exclusively supplies input.
                    _human.GetComponent<PlayerController>().isLocalPlayer = false;
                    foreach (var ai in UnityEngine.Object.FindObjectsByType<AIController>(FindObjectsSortMode.None))
                        ai.enabled = false;
                    foreach (var agent in UnityEngine.Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None))
                        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
                    _movementStart = human.transform.position;
                    _movementTime = EditorApplication.timeSinceStartup;
                    _stage = 3;
                }
                else if (_stage == 3)
                {
                    _human.SubmitMovement(new Vector2(0f, 100f));
                    if (EditorApplication.timeSinceStartup - _movementTime < 0.75) return;
                    float distance = Vector3.Distance(_movementStart, _human.transform.position);
                    Check(distance > 0.01f, "Server physics moves human from input");
                    Check(distance < _human.GetComponent<PlayerController>().moveSpeed * 1.5f, "Oversized input cannot multiply movement speed");
                    _movementTime = EditorApplication.timeSinceStartup;
                    _stage = 4;
                }
                else if (_stage == 4)
                {
                    if (EditorApplication.timeSinceStartup - _movementTime < 0.5) return;
                    _movementStart = _human.transform.position;
                    _movementTime = EditorApplication.timeSinceStartup;
                    _stage = 5;
                }
                else if (_stage == 5)
                {
                    if (EditorApplication.timeSinceStartup - _movementTime < 0.3) return;
                    Check(Vector3.Distance(_movementStart, _human.transform.position) < 0.1f, "Stale input stops movement");
                    GameManager.Instance.ChangeState(GameManager.GameState.Result);
                    Check(!_human.RequestAbility(), "Server rejects actions after result");
                    Complete(0, "PASS: host startup, network scene load, 8 spawned characters, 7 AI bots, ready-to-playing transition, server physics, ability phase/cooldown guards, bounded movement and stale-input stop.");
                }
            }
            catch (Exception e) { Complete(1, "FAIL: " + e); }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Complete(int exitCode, string report)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/network-smoke.txt", report);
            NetworkSessionManager.Shutdown();
            Debug.Log(report);
            EditorApplication.Exit(exitCode);
        }
    }
}
