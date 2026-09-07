using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.GamePanelElement
{
    // The selected prefab defines the aspect ratio, spacing and typography.
    // Derive height from the available width so resizing preserves that ratio.
    public sealed class PlayerListGrid : GridLayoutGroup
    {
        [SerializeField, HideInInspector] private float _cellAspectRatio = 8f / 3f;

        public void SetTemplate(PlayerListItem prefab, int columns)
        {
            constraint = Constraint.FixedColumnCount;
            constraintCount = columns;
            startAxis = Axis.Horizontal;
            startCorner = Corner.UpperLeft;
            spacing = prefab.ListSpacing;
            var template = ((RectTransform)prefab.transform).rect;
            _cellAspectRatio = template.width / template.height;
            cellSize = new Vector2(cellSize.x, template.height);
            SetDirty();
        }

        public override void CalculateLayoutInputHorizontal()
        {
            int columns = Mathf.Max(1, constraintCount);
            float available = rectTransform.rect.width - padding.horizontal - spacing.x * (columns - 1);
            float width = Mathf.Max(1, available / columns);
            cellSize = new Vector2(width, width / _cellAspectRatio);
            base.CalculateLayoutInputHorizontal();
        }
    }
}
