using UnityEngine;

namespace MoraeGames.Core
{
    /// <summary>
    /// Tints renderers via MaterialPropertyBlock instead of Renderer.material,
    /// which silently instantiates a material copy per access (a steady memory
    /// leak on mobile). One static MPB is reused — zero allocation per call.
    /// </summary>
    public static class RendererTint
    {
        private static MaterialPropertyBlock _mpb;

        // URP Lit uses _BaseColor; set legacy _Color too for non-URP shaders
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Applies a tint color to the renderer without instantiating its material.</summary>
        public static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _mpb.SetColor(ColorId, color);
            renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>Removes the tint override, restoring the shared material's own color.</summary>
        public static void ClearColor(Renderer renderer)
        {
            if (renderer == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            _mpb.Clear();
            renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>The renderer's base color from its shared material (no instantiation).</summary>
        public static Color GetBaseColor(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterial == null) return Color.white;
            Material mat = renderer.sharedMaterial;
            if (mat.HasProperty(BaseColorId)) return mat.GetColor(BaseColorId);
            if (mat.HasProperty(ColorId)) return mat.GetColor(ColorId);
            return Color.white;
        }
    }
}
