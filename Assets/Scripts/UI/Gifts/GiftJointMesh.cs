using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Gifts
{
    /// <summary>Blends a sleeve from its fixed shoulder to the animated arm, keeping its attachment closed.</summary>
    [RequireComponent(typeof(Image))]
    internal sealed class GiftJointMesh : BaseMeshEffect
    {
        private const int Columns = 12;
        private const int Rows = 8;
        private float _reach;
        private float _direction;
        private float _angle;

        public void Configure(float reach, float direction)
        {
            _reach = Mathf.Max(1f, reach);
            _direction = direction < 0f ? -1f : 1f;
            graphic.SetVerticesDirty();
        }

        public void SetAngle(float angle)
        {
            _angle = angle;
            graphic.SetVerticesDirty();
        }

        internal static Vector3 Deform(Vector3 point, float angle, float reach, float direction)
        {
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((point.x * direction - reach * 0.12f) / reach));
            // The inverse transform cancels the arm's rotation at the shoulder; the hand follows it fully.
            return Vector3.Lerp(Quaternion.Euler(0f, 0f, -angle) * point, point, weight);
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount != 4 || _reach <= 0f) return;
            var bl = UIVertex.simpleVert;
            var tl = UIVertex.simpleVert;
            var tr = UIVertex.simpleVert;
            var br = UIVertex.simpleVert;
            mesh.PopulateUIVertex(ref bl, 0);
            mesh.PopulateUIVertex(ref tl, 1);
            mesh.PopulateUIVertex(ref tr, 2);
            mesh.PopulateUIVertex(ref br, 3);
            mesh.Clear();
            for (int y = 0; y <= Rows; y++)
            {
                float v = (float)y / Rows;
                for (int x = 0; x <= Columns; x++)
                {
                    float u = (float)x / Columns;
                    var vertex = bl;
                    var lower = Vector3.Lerp(bl.position, br.position, u);
                    var upper = Vector3.Lerp(tl.position, tr.position, u);
                    vertex.position = Deform(Vector3.Lerp(lower, upper, v), _angle, _reach, _direction);
                    vertex.uv0 = Vector4.Lerp(Vector4.Lerp(bl.uv0, br.uv0, u), Vector4.Lerp(tl.uv0, tr.uv0, u), v);
                    mesh.AddVert(vertex);
                }
            }
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    int i = y * (Columns + 1) + x;
                    mesh.AddTriangle(i, i + Columns + 1, i + Columns + 2);
                    mesh.AddTriangle(i + Columns + 2, i + 1, i);
                }
            }
        }
    }
}
