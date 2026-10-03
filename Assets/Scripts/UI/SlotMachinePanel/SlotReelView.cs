using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class SlotReelView : MonoBehaviour
    {
        [SerializeField] private Image[] _cells;
        [SerializeField] private Sprite[] _symbols;
        [SerializeField] private float _spacing = 210f;

        public void SetTravel(float travel, int seed)
        {
            int step = Mathf.FloorToInt(travel);
            float fraction = travel - step;
            for (int i = 0; i < _cells.Length; i++)
            {
                int offset = i - _cells.Length / 2;
                int symbol = ((seed + step + offset) % _symbols.Length + _symbols.Length) % _symbols.Length;
                _cells[i].sprite = _symbols[symbol];
                _cells[i].rectTransform.anchoredPosition = new Vector2(0f, (offset - fraction) * _spacing);
            }
        }
    }
}
