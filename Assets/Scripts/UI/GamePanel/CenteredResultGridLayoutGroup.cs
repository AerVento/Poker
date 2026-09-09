using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.GamePanelElement
{
    // Keep four fixed-size cards per full row; spread a short row evenly around its center.
    public sealed class CenteredResultGridLayoutGroup : GridLayoutGroup
    {
        public override void SetLayoutVertical()
        {
            base.SetLayoutVertical();
            if (constraint != Constraint.FixedColumnCount || startAxis != Axis.Horizontal ||
                startCorner != Corner.UpperLeft || rectChildren.Count == 0) return;
            int count = rectChildren.Count % constraintCount;
            if (count == 0) return;
            float availableWidth = rectTransform.rect.width - padding.horizontal;
            float gap = Mathf.Max(0f, (availableWidth - count * cellSize.x) / (count + 1));
            int first = rectChildren.Count - count;
            for (int i = 0; i < count; i++)
                SetChildAlongAxis(rectChildren[first + i], 0,
                    padding.left + gap + i * (cellSize.x + gap), cellSize.x);
        }
    }
}
