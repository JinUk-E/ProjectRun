using UnityEngine;
using UnityEngine.UI;

namespace MoraeGames.UI
{
    /// <summary>
    /// Vertex-color gradient for any UGUI Graphic (Image, RawImage), the same way
    /// Shadow/Outline work — no shaders, no extra draw calls. The gradient MULTIPLIES
    /// the existing vertex color, so Image.color tints and Button press transitions
    /// still compose on top. Cost is only paid on mesh rebuilds, not per frame.
    /// </summary>
    [AddComponentMenu("UI/Effects/UI Gradient")]
    [RequireComponent(typeof(Graphic))]
    public class UIGradient : BaseMeshEffect
    {
        public enum Direction
        {
            Vertical,   // topColor at top, bottomColor at bottom
            Horizontal  // topColor at left, bottomColor at right
        }

        public Direction direction = Direction.Vertical;
        public Color topColor = Color.white;
        public Color bottomColor = new Color(0.72f, 0.72f, 0.72f, 1f);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            UIVertex vertex = default;

            // Find the mesh extent along the gradient axis (sliced images have a 9-patch grid)
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float v = direction == Direction.Vertical ? vertex.position.y : vertex.position.x;
                if (v < min) min = v;
                if (v > max) max = v;
            }

            float range = max - min;
            if (range <= 0f) return;

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float v = direction == Direction.Vertical ? vertex.position.y : vertex.position.x;
                float t = (v - min) / range;

                Color gradient = direction == Direction.Vertical
                    ? Color.Lerp(bottomColor, topColor, t)
                    : Color.Lerp(topColor, bottomColor, t);

                vertex.color = (Color)vertex.color * gradient;
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
