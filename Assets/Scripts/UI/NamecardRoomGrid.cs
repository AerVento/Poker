using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class NamecardRoomGrid : MonoBehaviour
    {
        private void OnEnable() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();

        private void Fit()
        {
            var grid = GetComponent<GridLayoutGroup>();
            if (grid == null) return;
            float width = ((RectTransform)transform).rect.width - grid.padding.horizontal;
            if (width <= 0) return;
            int columns = width >= 900 ? 3 : width >= 600 ? 2 : 1;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            float cardWidth = (width - grid.spacing.x * (columns - 1)) / columns;
            var size = new Vector2(cardWidth, cardWidth / NamecardLayout.Aspect);
            if (grid.cellSize != size) grid.cellSize = size;
        }
    }
}
