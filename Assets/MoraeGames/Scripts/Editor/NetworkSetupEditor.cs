using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace MoraeGames.Editor
{
    /// <summary>
    /// Places the NGO NetworkManager into the MainMenu (boot) scene.
    /// NetworkManager marks itself DontDestroyOnLoad, so one instance in the boot
    /// scene serves the whole session. Character prefabs with NetworkObject are
    /// picked up by NGO's auto-generated DefaultNetworkPrefabs list.
    /// </summary>
    public class NetworkSetupEditor : MonoBehaviour
    {
        [MenuItem("MoraeGames/Setup Network Manager")]
        public static void SetupNetworkManager()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene.name != "MainMenu")
            {
                Debug.Log("Loading MainMenu scene for network setup...");
                EditorSceneManager.OpenScene("Assets/MoraeGames/Scenes/MainMenu.unity");
                currentScene = SceneManager.GetActiveScene();
            }

            // Replace any previous setup for idempotency
            NetworkManager existing = FindFirstObjectByType<NetworkManager>();
            if (existing != null) DestroyImmediate(existing.gameObject);

            GameObject nmObj = new GameObject("NetworkManager");
            NetworkManager networkManager = nmObj.AddComponent<NetworkManager>();
            UnityTransport transport = nmObj.AddComponent<UnityTransport>();

            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.EnableSceneManagement = true;

            // Dedicated-server lifecycle driver (self-destructs on client builds)
            GameObject bootstrapObj = GameObject.Find("ServerBootstrap");
            if (bootstrapObj != null) DestroyImmediate(bootstrapObj);
            bootstrapObj = new GameObject("ServerBootstrap");
            bootstrapObj.AddComponent<MoraeGames.Network.ServerBootstrap>();

            EditorSceneManager.MarkSceneDirty(currentScene);
            Debug.Log("[NetworkSetup] NetworkManager placed in MainMenu scene (persists across scenes). " +
                      "Regenerate character prefabs so they carry NetworkObject components.");
        }
    }
}
