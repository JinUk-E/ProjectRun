using UnityEditor;
using UnityEngine;
using MoraeGames.Core;

namespace MoraeGames.Editor
{
    /// <summary>
    /// Creates the editable GameBalance asset at Resources/GameBalance so designers
    /// can tune gameplay values in the inspector without code changes.
    /// </summary>
    public class BalanceSetupEditor : MonoBehaviour
    {
        private const string AssetPath = "Assets/MoraeGames/Resources/GameBalance.asset";

        [MenuItem("MoraeGames/Setup Game Balance Asset")]
        public static void CreateBalanceAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/MoraeGames/Resources"))
                AssetDatabase.CreateFolder("Assets/MoraeGames", "Resources");

            GameBalance existing = AssetDatabase.LoadAssetAtPath<GameBalance>(AssetPath);
            if (existing != null)
            {
                Debug.Log($"[BalanceSetup] GameBalance asset already exists at {AssetPath}.");
                Selection.activeObject = existing;
                return;
            }

            GameBalance balance = ScriptableObject.CreateInstance<GameBalance>();
            AssetDatabase.CreateAsset(balance, AssetPath);
            AssetDatabase.SaveAssets();

            // Pick up the new asset immediately (facade caches on main thread only)
            GameConstants.ReloadBalance();

            Selection.activeObject = balance;
            Debug.Log($"[BalanceSetup] GameBalance asset created at {AssetPath}. Tune values in the inspector.");
        }
    }
}
