using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Gifts
{
    // Layered glass and liquid mesh; the free surface compensates for the cup's tilt.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ChampagneGlassGraphic : MaskableGraphic
    {
        private float _fill;
        private float _time;

        /// <summary>Liquid level, 0 empty to 1 full.</summary>
        public float Fill
        {
            get => _fill;
            set { _fill = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        /// <summary>Playback time driving bubbles and the liquid's ripple.</summary>
        public float Clock
        {
            get => _time;
            set { _time = value; SetVerticesDirty(); }
        }

        // The free surface stays level against the glass's own rotation.
        private float Tilt => Mathf.DeltaAngle(0f, rectTransform.localEulerAngles.z);

        private static float Width(float y)
            => 0.43f * Mathf.Sqrt(Mathf.Clamp01((y - 0.39f) / 0.26f)) * Mathf.Lerp(1f, 0.88f, Mathf.InverseLerp(0.65f, 0.95f, y));

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            const int slices = 48;
            float surface = Mathf.Lerp(0.405f, 0.875f, _fill);
            float slope = -Mathf.Tan(Tilt * Mathf.Deg2Rad) * rect.width / rect.height;
            for (int i = 0; i < slices; i++)
            {
                float y0 = Mathf.Lerp(0.405f, 0.95f, i / (float)slices);
                float y1 = Mathf.Lerp(0.405f, 0.95f, (i + 1f) / slices);
                float w0 = Width(y0), w1 = Width(y1);
                Quad(vh, rect, new Vector2(-w0, y0), new Vector2(w0, y0), new Vector2(w1, y1), new Vector2(-w1, y1), new Color(0.70f, 0.89f, 0.86f, 0.13f));
                Edge(vh, rect, -w0, y0, -w1, y1, 0.018f, new Color(0.89f, 1f, 0.98f, 0.8f));
                Edge(vh, rect, w0, y0, w1, y1, 0.014f, new Color(1f, 0.91f, 0.70f, 0.7f));
            }
            if (_fill > 0.001f)
            {
                // Thin vertical strips make an arbitrary sloping surface fit the bowl exactly.
                for (int i = 0; i < 70; i++)
                {
                    float x0 = Mathf.Lerp(-0.405f, 0.405f, i / 70f);
                    float x1 = x0 + 0.81f / 70f;
                    float x = (x0 + x1) * 0.5f;
                    float bottom = 0.39f + 0.26f * Mathf.Pow(Mathf.Abs(x) / 0.43f, 2f);
                    float top = Mathf.Min(0.925f, surface + slope * x + Mathf.Sin(_time * 5f + x * 8f) * 0.004f);
                    if (top <= bottom || Mathf.Abs(x) > Width(top) - 0.012f) continue;
                    var tint = Color.Lerp(new Color(0.77f, 0.42f, 0.06f, 0.78f), new Color(1f, 0.83f, 0.32f, 0.85f), 1f - Mathf.Abs(x) * 1.8f);
                    Quad(vh, rect, new Vector2(x0, bottom), new Vector2(x1, bottom), new Vector2(x1, top), new Vector2(x0, top), tint);
                    Quad(vh, rect, new Vector2(x0, top - 0.01f), new Vector2(x1, top - 0.01f), new Vector2(x1, top), new Vector2(x0, top), new Color(1f, 0.95f, 0.71f, 0.9f));
                }
                for (int i = 0; i < 18; i++)
                {
                    float y = Mathf.Lerp(0.46f, surface - 0.02f, Mathf.Repeat(_time * (0.2f + i % 3 * 0.06f) + i * 0.173f, 1f));
                    float x = Mathf.Sin(i * 12.7f) * Width(y) * 0.70f;
                    if (y < surface + slope * x - 0.012f)
                        Ellipse(vh, rect, x, y, 0.009f + i % 3 * 0.003f, 0.0045f + i % 3 * 0.001f, new Color(1f, 0.98f, 0.79f, 0.7f));
                }
            }
            // Front reflections, rim, stem and foot remain visible over the wine.
            Edge(vh, rect, -0.29f, 0.58f, -0.32f, 0.88f, 0.035f, new Color(1f, 1f, 0.92f, 0.36f));
            Edge(vh, rect, 0.26f, 0.67f, 0.28f, 0.87f, 0.014f, new Color(1f, 1f, 1f, 0.55f));
            Ellipse(vh, rect, 0f, 0.95f, Width(0.95f) + 0.01f, 0.014f, new Color(0.95f, 1f, 0.98f, 0.85f), true);
            Edge(vh, rect, -0.013f, 0.085f, -0.013f, 0.408f, 0.04f, new Color(0.82f, 0.96f, 0.93f, 0.75f));
            Ellipse(vh, rect, 0f, 0.07f, 0.30f, 0.025f, new Color(0.72f, 0.90f, 0.86f, 0.23f));
            Ellipse(vh, rect, 0f, 0.07f, 0.30f, 0.025f, new Color(0.93f, 1f, 0.97f, 0.75f), true);
        }

        private static Vector2 Point(Rect rect, Vector2 p) => new Vector2(rect.center.x + p.x * rect.width, rect.yMin + p.y * rect.height);

        private static void Quad(VertexHelper vh, Rect rect, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int n = vh.currentVertCount;
            vh.AddVert(Point(rect, a), tint, Vector2.zero);
            vh.AddVert(Point(rect, b), tint, Vector2.zero);
            vh.AddVert(Point(rect, c), tint, Vector2.zero);
            vh.AddVert(Point(rect, d), tint, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
            vh.AddTriangle(n, n + 2, n + 3);
        }

        private static void Edge(VertexHelper vh, Rect rect, float x0, float y0, float x1, float y1, float width, Color tint)
            => Quad(vh, rect, new Vector2(x0 - width / 2f, y0), new Vector2(x0 + width / 2f, y0), new Vector2(x1 + width / 2f, y1), new Vector2(x1 - width / 2f, y1), tint);

        private static void Ellipse(VertexHelper vh, Rect rect, float x, float y, float rx, float ry, Color tint, bool ring = false)
        {
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2f / 28f, b = (i + 1) * Mathf.PI * 2f / 28f;
                var p = new Vector2(x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry);
                var q = new Vector2(x + Mathf.Cos(b) * rx, y + Mathf.Sin(b) * ry);
                var center = new Vector2(x, y);
                Quad(vh, rect, p, q, ring ? Vector2.Lerp(center, q, 0.86f) : center, ring ? Vector2.Lerp(center, p, 0.86f) : center, tint);
            }
        }
    }
}
