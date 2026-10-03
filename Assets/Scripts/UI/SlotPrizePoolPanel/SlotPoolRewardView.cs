using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class SlotPoolRewardView : MonoBehaviour
    {
        [SerializeField] private RewardItemView _item;
        [SerializeField] private TextMeshProUGUI _poolOdds;
        [SerializeField] private TextMeshProUGUI _drawOdds;
        [SerializeField] private CanvasGroup _group;
        public void Present(SlotRewardEntry entry, SlotLotteryDisclosure disclosure)
        {
            _item.Bind(entry.Reward);
            bool owned = SlotLotteryDisclosure.IsOwned(entry);
            // Owned rewards stay listed so the pool is complete, but are dimmed and never drawn.
            if (_group != null) _group.alpha = owned ? 0.45f : 1f;
            double inside = disclosure.PoolProbability(entry);
            _poolOdds.text = owned ? "已拥有" : "奖池内 " + SlotProbabilityText.Format(inside);
            _drawOdds.text = "单次抽中 " + SlotProbabilityText.Format(inside * disclosure.Probability(disclosure.Tier));
        }
    }
}
