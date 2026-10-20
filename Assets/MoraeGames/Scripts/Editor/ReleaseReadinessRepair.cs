using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using MoraeGames.Network;
using MoraeGames.Player;
using MoraeGames.Game;

namespace MoraeGames.Editor
{
    /// <summary>Non-destructive migration of existing scene and character wiring.</summary>
    public static class ReleaseReadinessRepair
    {
        [MenuItem("MoraeGames/Repair Release Wiring")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
                if (list == null) throw new InvalidOperationException("Missing default network prefab list.");
                foreach (string path in Directory.GetFiles("Assets/MoraeGames/Prefabs/Characters", "*.prefab"))
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        if (root.GetComponent<PlayerController>() == null) continue;
                        Ensure<NetworkObject>(root);
                        Ensure<OwnerNetworkTransform>(root);
                        Ensure<NetworkCharacter>(root);
                        if (root.GetComponent<RobberController>() != null) Ensure<NetworkRobberSync>(root);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!list.Contains(prefab)) list.Add(new NetworkPrefab { Prefab = prefab });
                }
                EditorUtility.SetDirty(list);

                var menu = EditorSceneManager.OpenScene("Assets/MoraeGames/Scenes/MainMenu.unity");
                var nm = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
                if (nm == null) nm = new GameObject("NetworkManager").AddComponent<NetworkManager>();
                var transport = Ensure<UnityTransport>(nm.gameObject);
                nm.NetworkConfig.NetworkTransport = transport;
                nm.NetworkConfig.ConnectionApproval = true;
                nm.NetworkConfig.EnableSceneManagement = true;
                if (!nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(list))
                    nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
                if (UnityEngine.Object.FindFirstObjectByType<ServerBootstrap>() == null)
                    new GameObject("ServerBootstrap").AddComponent<ServerBootstrap>();
                EditorSceneManager.MarkSceneDirty(menu);
                EditorSceneManager.SaveScene(menu);

                var game = EditorSceneManager.OpenScene("Assets/MoraeGames/Scenes/InGame.unity");
                var sync = UnityEngine.Object.FindFirstObjectByType<NetworkMatchSync>();
                if (sync == null) new GameObject("NetworkMatchSync").AddComponent<NetworkMatchSync>();
                EditorSceneManager.MarkSceneDirty(game);
                EditorSceneManager.SaveScene(game);
                BuildSetupEditor.SetupScenes();
                AssetDatabase.SaveAssets();
                Verify();
            }
            finally
            {
                if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static T Ensure<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();

        /// <summary>Regression checks for terminal timers, hostile input and saved asset wiring.</summary>
        public static void Verify()
        {
            Require(NetworkMatchSync.ShouldReplicateTimer(0.1f, 0f, 0.2f), "Countdown must send terminal zero");
            Require(NetworkMatchSync.ShouldReplicateTimer(0.4f, 0f, 0.5f), "Match timer must send terminal zero");
            Require(!NetworkMatchSync.ShouldReplicateTimer(0f, 0f, 0.2f), "Unchanged zero should not resend");
            Require(NetworkCharacter.SanitizeInput(new Vector2(float.NaN, 1f)) == Vector2.zero, "Reject NaN");
            Require(NetworkCharacter.SanitizeInput(new Vector2(1f, float.PositiveInfinity)) == Vector2.zero, "Reject infinity");
            Require(NetworkCharacter.SanitizeInput(new Vector2(100f, 100f)).magnitude <= 1.00001f, "Clamp diagonal input");
            Require(NetworkCharacter.SanitizeInput(new Vector2(0.25f, 0f)).x == 0.25f, "Preserve analog input");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            Require(scenes.Length == 3 && scenes[0].path.EndsWith("MainMenu.unity"), "Boot scene order");
            foreach (var scene in scenes) Require(File.Exists(scene.path), "Missing build scene: " + scene.path);
            int count = 0;
            foreach (string path in Directory.GetFiles("Assets/MoraeGames/Prefabs/Characters", "*.prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<PlayerController>() == null) continue;
                Require(prefab.GetComponent<NetworkCharacter>() != null, "Missing character sync: " + path);
                Require(prefab.GetComponent<OwnerNetworkTransform>().IsServerAuthoritative(), "Client position authority: " + path);
                if (prefab.GetComponent<RobberController>() != null)
                    Require(prefab.GetComponent<NetworkRobberSync>() != null, "Missing robber sync: " + path);
                count++;
            }
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/release-regression.txt", "PASS: terminal timers, finite/clamped input, analog input, build scenes, " + count + " character prefabs.\n");
            Debug.Log("[ReleaseReadiness] Regression checks passed; character prefabs: " + count);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        /// <summary>Builds a local Windows verification player; never publishes or uploads.</summary>
        public static void BuildVerificationPlayer()
        {
            Verify();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/ReleaseVerification/CopsAndRobbers.exe",
                options = BuildOptions.None
            });
            File.WriteAllText("TestResults/player-build.txt", report.summary.result + "; errors=" + report.summary.totalErrors);
            Require(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded, "Player build failed");
        }
    }
}
