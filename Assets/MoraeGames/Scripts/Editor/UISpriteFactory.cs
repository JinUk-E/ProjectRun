using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoraeGames.Editor
{
    /// <summary>
    /// Generates and caches procedural UI sprites (rounded rectangle, anti-aliased
    /// circle) as PNG assets so the UI doesn't rely on Unity's built-in placeholder
    /// sprites. Idempotent: existing assets are reused.
    /// </summary>
    public static class UISpriteFactory
    {
        private const string Folder = "Assets/MoraeGames/UI/Sprites";

        /// <summary>White 9-slice rounded rectangle (radius 32px on a 128px texture).</summary>
        public static Sprite RoundedRect => LoadOrGenerate("RoundedRect", GenerateRoundedRect, new Vector4(40, 40, 40, 40));

        /// <summary>White anti-aliased filled circle (256px).</summary>
        public static Sprite Circle => LoadOrGenerate("SoftCircle", GenerateCircle, Vector4.zero);

        private static Sprite LoadOrGenerate(string name, System.Func<Texture2D> generator, Vector4 border)
        {
            string path = $"{Folder}/{name}.png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder("Assets/MoraeGames/UI"))
                AssetDatabase.CreateFolder("Assets/MoraeGames", "UI");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/MoraeGames/UI", "Sprites");

            Texture2D texture = generator();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = border;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Texture2D GenerateRoundedRect()
        {
            const int size = 128;
            const float radius = 32f;
            const float aa = 2f; // anti-alias falloff in pixels

            Texture2D texture = NewTexture(size);
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to a rounded box centered in the texture
                    Vector2 p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                    Vector2 q = new Vector2(
                        Mathf.Abs(p.x) - (half - radius - aa),
                        Mathf.Abs(p.y) - (half - radius - aa));
                    float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
                    float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
                    float distance = outside + inside - radius;

                    float alpha = Mathf.Clamp01(0.5f - distance / aa);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D GenerateCircle()
        {
            const int size = 256;
            const float aa = 3f;

            Texture2D texture = NewTexture(size);
            float half = size * 0.5f;
            float radius = half - aa - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude - radius;
                    float alpha = Mathf.Clamp01(0.5f - distance / aa);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }
    }
}
