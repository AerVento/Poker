using System.Collections.Generic;
using Game.Network.Accounts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class SlotBatchResultView : MonoBehaviour
    {
        [SerializeField] private RewardItemView _reward;
        [SerializeField] private RectTransform _items;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private LayoutElement _layout;
        [SerializeField] private Image _surface;
        [SerializeField] private Image _border;
        [SerializeField] private Image _tierIcon;
        [SerializeField] private Sprite[] _tierSprites;
        [SerializeField] private GameObject _empty;
        [SerializeField] private TextMeshProUGUI _index;
        [SerializeField] private TextMeshProUGUI _tier;
        private readonly List<RewardItemView> _views = new List<RewardItemView>();

        public void Present(SlotDrawOutcome result, int index)
        {
            int count = result.Reward?.Entries.Count ?? 0;
            bool won = count > 0;
            _index.text = $"第 {index + 1} 抽";
            _tier.text = result.Reward != null && !won ? "已达持有上限" : won ? SlotProbabilityText.TierName(result.Tier) : "未中奖";
            _empty.SetActive(false);
            _reward.gameObject.SetActive(false);
            _scroll.gameObject.SetActive(won);
            _scroll.StopMovement();
            _items.anchoredPosition = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                if (i == _views.Count) _views.Add(Instantiate(_reward, _items));
                _views[i].gameObject.SetActive(true);
                _views[i].Bind(result.Reward.Entries[i]);
            }
            for (int i = count; i < _views.Count; i++) _views[i].gameObject.SetActive(false);
            _layout.preferredHeight = won ? 244f : 84f;
            Color accent = new Color(0.5f, 0.45f, 0.52f);
            Color surface = new Color(0.11f, 0.08f, 0.14f);
            if (won)
            {
                accent = result.Tier == SlotPrizeTier.Grand ? new Color(1f, 0.79f, 0.35f) :
                    result.Tier == SlotPrizeTier.Medium ? new Color(0.46f, 0.69f, 1f) : new Color(0.78f, 0.51f, 0.43f);
                surface = result.Tier == SlotPrizeTier.Grand ? new Color(0.24f, 0.07f, 0.24f) :
                    result.Tier == SlotPrizeTier.Medium ? new Color(0.12f, 0.10f, 0.24f) : new Color(0.22f, 0.065f, 0.12f);
            }
            _tierIcon.gameObject.SetActive(won);
            if (won) _tierIcon.sprite = _tierSprites[Mathf.Clamp((int)result.Tier - 1, 0, _tierSprites.Length - 1)];
            _tier.color = accent;
            _border.color = accent;
            _surface.color = surface;
        }
    }
}
