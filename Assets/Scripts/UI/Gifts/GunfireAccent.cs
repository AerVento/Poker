using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Gifts
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GunfireAccent : MaskableGraphic
    {
        public enum Shape { Hole, Flash }
        public Shape Kind { get; set; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            int count = Kind == Shape.Hole ? 32 : 16;
            Color center = Kind == Shape.Hole ? new Color(0.025f, 0.035f, 0.035f) : new Color(1f, 0.99f, 0.8f);
            vh.AddVert(rect.center, center, Vector2.zero);
            for (int ring = 0; ring < 2; ring++)
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    float radius = ring == 0 ? 0.53f : (i % 4 == 0 ? 1f : 0.72f + 0.08f * Mathf.Sin(i * 7f));
                    if (Kind == Shape.Flash) radius = ring == 0 ? 0.25f : (i % 2 == 0 ? 1f : 0.23f);
                    var p = rect.center + Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), rect.size * (radius * 0.5f));
                    Color tint = Kind == Shape.Hole
                        ? ring == 0 ? center : new Color(0.66f, 0.61f, 0.47f, 0.8f)
                        : ring == 0 ? center : new Color(1f, 0.59f, 0.14f, 0.12f);
                    vh.AddVert(p, tint * color, Vector2.zero);
                }
            }
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                vh.AddTriangle(0, 1 + i, 1 + next);
                vh.AddTriangle(1 + i, 1 + count + i, 1 + next);
                vh.AddTriangle(1 + next, 1 + count + i, 1 + count + next);
            }
        }
    }
}
