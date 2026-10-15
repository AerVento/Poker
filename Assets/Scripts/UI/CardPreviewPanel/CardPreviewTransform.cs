using UnityEngine;

namespace Game.UI
{
    // Coordinates are local to the full-screen panel, with its centre at zero.
    public sealed class CardPreviewTransform
    {
        public float Scale { get; private set; } = 1f;
        public Vector2 Position { get; private set; }

        public void Reset()
        {
            Scale = 1f;
            Position = Vector2.zero;
        }

        public void Pan(Vector2 delta)
        {
            Position += delta;
        }

        public void Zoom(float factor, Vector2 previousCentre, Vector2 currentCentre, float maximumScale)
        {
            if (factor <= 0 || float.IsNaN(factor) || float.IsInfinity(factor))
                return;
            float nextScale = Mathf.Clamp(Scale * factor, 0.5f, Mathf.Max(0.5f, maximumScale));
            Position = currentCentre - (previousCentre - Position) * (nextScale / Scale);
            Scale = nextScale;
        }
    }
}
