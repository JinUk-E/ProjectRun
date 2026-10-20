using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MoraeGames.Editor
{
    public class BuildSetupEditor : MonoBehaviour
    {
        [MenuItem("MoraeGames/Setup Build Scenes")]
        public static void SetupScenes()
        {
            // ONLY the three game scenes, in boot order. Anything else (e.g. the
            // template SampleScene sitting at index 0) must not ship — index 0 is
            // the boot scene.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/MoraeGames/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/MoraeGames/Scenes/Lobby.unity", true),
                new EditorBuildSettingsScene("Assets/MoraeGames/Scenes/InGame.unity", true)
            };

            // Mobile player settings for internal test builds:
            // the UI is designed landscape (3840x2160 reference), so lock orientation
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // A bundle identifier is required for Android/iOS builds
            if (string.IsNullOrEmpty(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)))
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.moraegames.copsandrobbers");
            if (string.IsNullOrEmpty(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS)))
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.moraegames.copsandrobbers");

            Debug.Log("[BuildSetup] Build scenes set (MainMenu → Lobby → InGame), " +
                      "orientation locked to landscape, bundle identifiers ensured.");
        }
    }
}
