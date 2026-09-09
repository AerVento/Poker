using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Gifts
{
    // Small texture-free accents; the main egg and impact use painted sprites.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RottenEggAccent : MaskableGraphic
    {
        public enum Shape { Disc, Drop, Wisp, Ring }
        [SerializeField] private Shape _shape;
        public Shape Kind { get => _shape; set { _shape = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            const int segments = 48;
            if (_shape == Shape.Wisp)
            {
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float x = Mathf.Sin(t * Mathf.PI * 2f) * rect.width * 0.22f;
                    float width = rect.width * 0.13f * Mathf.Sin(t * Mathf.PI);
                    var tint = color;
                    tint.a *= Mathf.Sin(t * Mathf.PI);
                    vh.AddVert(new Vector3(rect.center.x + x - width, rect.yMin + rect.height * t), tint, Vector2.zero);
                    vh.AddVert(new Vector3(rect.center.x + x + width, rect.yMin + rect.height * t), tint, Vector2.zero);
                    if (i > 0)
                    {
                        int a = i * 2;
                        vh.AddTriangle(a - 2, a - 1, a);
                        vh.AddTriangle(a - 1, a + 1, a);
                    }
                }
                return;
            }
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (_shape == Shape.Drop)
                    p.x *= 0.65f - p.y * 0.35f;
                Vector2 outside = rect.center + Vector2.Scale(p, rect.size * 0.5f);
                Vector2 inside = _shape == Shape.Ring ? rect.center + Vector2.Scale(p, rect.size * 0.46f) : rect.center;
                vh.AddVert(outside, color, Vector2.zero);
                vh.AddVert(inside, color, Vector2.zero);
                if (i > 0)
                {
                    int n = i * 2;
                    vh.AddTriangle(n - 2, n - 1, n);
                    vh.AddTriangle(n - 1, n + 1, n);
                }
            }
        }
    }
}
