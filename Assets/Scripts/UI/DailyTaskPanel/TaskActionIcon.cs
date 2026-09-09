using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // Vector geometry keeps both icons crisp and independent of font glyph coverage.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TaskActionIcon : MaskableGraphic
    {
        private bool _refresh;
        public void Display(bool refresh) { _refresh = refresh; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var center = rectTransform.rect.center;
            var radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.33f;
            if (!_refresh)
            {
                Line(vertices, center + new Vector2(-radius, -radius), center + new Vector2(radius, radius));
                Line(vertices, center + new Vector2(-radius, radius), center + new Vector2(radius, -radius));
                return;
            }
            for (var i = 0; i < 30; i++)
            {
                var a = (45 + i * 9) * Mathf.Deg2Rad;
                var b = (54 + i * 9) * Mathf.Deg2Rad;
                Line(vertices, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius);
            }
            var tip = center + new Vector2(0.7071f, 0.7071f) * radius;
            Line(vertices, tip, tip + new Vector2(0, -radius * 0.65f));
            Line(vertices, tip, tip + new Vector2(-radius * 0.65f, 0));
        }

        private void Line(VertexHelper vertices, Vector2 a, Vector2 b)
        {
            var delta = (b - a).normalized;
            var normal = new Vector2(-delta.y, delta.x) * 1.6f;
            var index = vertices.currentVertCount;
            vertices.AddVert(a - normal, color, Vector2.zero);
            vertices.AddVert(a + normal, color, Vector2.zero);
            vertices.AddVert(b + normal, color, Vector2.zero);
            vertices.AddVert(b - normal, color, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index, index + 2, index + 3);
        }
    }
}
