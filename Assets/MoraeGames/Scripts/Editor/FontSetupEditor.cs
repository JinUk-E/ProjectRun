using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace MoraeGames.Editor
{
    /// <summary>
    /// Creates a Korean-capable TMP font asset from the bundled NanumGothic TTF (OFL license).
    /// The asset uses Dynamic atlas population with multi-atlas support, so Hangul glyphs
    /// are rasterized on demand at runtime — no need to pre-bake thousands of characters.
    /// </summary>
    public class FontSetupEditor : MonoBehaviour
    {
        public const string FontAssetPath = "Assets/MoraeGames/Fonts/NanumGothicSDF.asset";
        private const string SourceFontPath = "Assets/MoraeGames/Fonts/NanumGothic-Regular.ttf";

        [MenuItem("MoraeGames/Setup Korean Font Asset")]
        public static void CreateKoreanFontAsset()
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[FontSetup] Source font not found at {SourceFontPath}. " +
                               "Make sure NanumGothic-Regular.ttf is imported.");
                return;
            }

            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                Debug.Log($"[FontSetup] Font asset already exists at {FontAssetPath}. Delete it first to regenerate.");
                return;
            }

            // Dynamic + multi-atlas: glyphs (including all Hangul) are added on demand.
            // 1024x1024 SDF atlas at 90pt sampling is a good quality/memory balance for mobile.
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError("[FontSetup] TMP_FontAsset.CreateFontAsset failed.");
                return;
            }

            fontAsset.name = "NanumGothicSDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            // Material and atlas texture must be saved as sub-assets, exactly like
            // the TMP Font Asset Creator window does
            fontAsset.atlasTexture.name = fontAsset.name + " Atlas";
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FontSetup] Korean TMP font asset created at {FontAssetPath}. " +
                      "Re-run the UI setup menus to apply it to all texts.");
        }
    }
}
