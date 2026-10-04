using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Gifts
{
    /// <summary>Small, tintable spell silhouettes with soft edges. Local +X is the flight direction.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpellBulletGraphic : MaskableGraphic
    {
        public enum Shape { Butterfly, Knife, Star, Beam, Orb, Cannon }
        private Shape _kind;
        public Shape Kind { get => _kind; set { _kind = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            if (_kind == Shape.Beam || _kind == Shape.Cannon)
            {
                // Longitudinal fade plus a narrow white core, without a hard rectangular edge.
                float[] bands = { -1f, -0.65f, -0.22f, 0f, 0.22f, 0.65f, 1f };
                for (int x = 0; x < 12; x++)
                {
                    float ramp = Mathf.Min(0.25f, 110f / Mathf.Max(1f, rect.width));
                    float t0 = _kind == Shape.Cannon ? CannonStation(x, ramp) : x / 12f;
                    float t1 = _kind == Shape.Cannon ? CannonStation(x + 1, ramp) : (x + 1) / 12f;
                    for (int y = 0; y < bands.Length - 1; y++)
                    {
                        int n = vh.currentVertCount;
                        BeamVertex(vh, rect, t0, bands[y]);
                        BeamVertex(vh, rect, t0, bands[y + 1]);
                        BeamVertex(vh, rect, t1, bands[y]);
                        BeamVertex(vh, rect, t1, bands[y + 1]);
                        vh.AddTriangle(n, n + 1, n + 2);
                        vh.AddTriangle(n + 1, n + 3, n + 2);
                    }
                }
                return;
            }
            if (_kind == Shape.Orb)
            {
                Disc(vh, rect, Vector2.zero, new Vector2(0.5f, 0.5f), 0, Tint(0f, 0f), Tint(0.2f, 0.15f));
                Disc(vh, rect, Vector2.zero, new Vector2(0.36f, 0.36f), 0, Tint(0f, 0.45f), Tint(0.65f, 1f));
                Disc(vh, rect, Vector2.zero, new Vector2(0.21f, 0.21f), 0, Tint(0.85f, 1f), Tint(1f, 1f));
                return;
            }
            for (int pass = 0; pass < 3; pass++)
            {
                float scale = pass == 0 ? 1f : pass == 1 ? 0.84f : 0.73f;
                Color edge = Tint(pass == 2 ? 0.25f : 0f, pass == 0 ? 0f : pass == 1 ? 0.35f : 0.9f);
                Color center = Tint(pass == 2 ? 0.95f : 0f, pass == 0 ? 0.22f : 1f);
                if (_kind == Shape.Star)
                {
                    var points = new Vector2[10];
                    for (int i = 0; i < points.Length; i++)
                    {
                        float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                        float r = (i % 2 == 0 ? 0.49f : 0.22f) * scale;
                        points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    }
                    Polygon(vh, rect, points, edge, center);
                }
                else if (_kind == Shape.Butterfly)
                {
                    // Four distinct tapered wings, two larger upper wings, and a slim body.
                    Disc(vh, rect, new Vector2(-0.2f, 0.16f) * scale, new Vector2(0.2f, 0.29f) * scale, 38f, edge, center);
                    Disc(vh, rect, new Vector2(0.2f, 0.16f) * scale, new Vector2(0.2f, 0.29f) * scale, -38f, edge, center);
                    Disc(vh, rect, new Vector2(-0.14f, -0.17f) * scale, new Vector2(0.14f, 0.2f) * scale, -25f, edge, center);
                    Disc(vh, rect, new Vector2(0.14f, -0.17f) * scale, new Vector2(0.14f, 0.2f) * scale, 25f, edge, center);
                    Disc(vh, rect, Vector2.zero, new Vector2(0.025f, 0.23f) * scale, 0, center, center);
                }
                else
                {
                    Polygon(vh, rect, new[] { new Vector2(-0.2f, -0.15f) * scale,
                        new Vector2(0.18f, -0.12f) * scale, new Vector2(0.5f, 0f) * scale,
                        new Vector2(0.18f, 0.12f) * scale, new Vector2(-0.2f, 0.15f) * scale }, edge, center);
                    Polygon(vh, rect, new[] { new Vector2(-0.48f, -0.065f) * scale, new Vector2(-0.2f, -0.065f) * scale,
                        new Vector2(-0.2f, 0.065f) * scale, new Vector2(-0.48f, 0.065f) * scale }, Tint(0, 1), Tint(0.3f, 1));
                    Polygon(vh, rect, new[] { new Vector2(-0.23f, -0.25f) * scale, new Vector2(-0.16f, -0.25f) * scale,
                        new Vector2(-0.16f, 0.25f) * scale, new Vector2(-0.23f, 0.25f) * scale }, edge, center);
                }
            }
        }

        private Color Tint(float white, float alpha)
        {
            var c = Color.Lerp(color, Color.white, white);
            c.a = color.a * alpha;
            return c;
        }

        private static float CannonStation(int index, float ramp) => index <= 4
            ? ramp * index / 4f : ramp + (1f - ramp) * (index - 4) / 8f;

        private void BeamVertex(VertexHelper vh, Rect rect, float t, float band)
        {
            float ramp = Mathf.Min(0.25f, 110f / Mathf.Max(1f, rect.width));
            float width = _kind == Shape.Cannon ? Mathf.SmoothStep(0.045f, 0.5f, Mathf.Clamp01(t / ramp))
                : Mathf.Lerp(0.12f, 0.5f, t);
            var c = Tint(1f - Mathf.Clamp01(Mathf.Abs(band) * 3f),
                (1f - Mathf.Pow(Mathf.Abs(band), 1.5f)) * Mathf.Clamp01(t * 16f) * Mathf.Clamp01((1f - t) * 8f));
            vh.AddVert(new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, t), rect.center.y + rect.height * width * band), c, Vector2.zero);
        }

        private static void Disc(VertexHelper vh, Rect rect, Vector2 center, Vector2 radius, float degrees, Color edge, Color inner)
        {
            var points = new Vector2[24];
            float angle = degrees * Mathf.Deg2Rad, c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * Mathf.PI * 2f / points.Length;
                var p = new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y);
                points[i] = center + new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
            }
            Polygon(vh, rect, points, edge, inner);
        }

        private static void Polygon(VertexHelper vh, Rect rect, Vector2[] points, Color edge, Color inner)
        {
            int start = vh.currentVertCount;
            var center = Vector2.zero;
            foreach (var p in points) center += p;
            center /= points.Length;
            vh.AddVert(rect.center + Vector2.Scale(center, rect.size), inner, Vector2.zero);
            foreach (var p in points) vh.AddVert(rect.center + Vector2.Scale(p, rect.size), edge, Vector2.zero);
            for (int i = 0; i < points.Length; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % points.Length);
        }
    }
}
