using Game.Network.Accounts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class LeaderboardRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _rank;
        [SerializeField] private TextMeshProUGUI _name;
        [SerializeField] private TextMeshProUGUI _chips;
        [SerializeField] private Image _background;
        [SerializeField] private NamecardAppearance _namecard;

        public void Show(LeaderboardEntry entry)
        {
            _namecard.Present(entry.NamecardId);
            _rank.text = entry.Rank > 0 ? entry.Rank.ToString() : "—";
            _name.text = entry.UserName == null ? "我的排名" : entry.UserName + (entry.IsSelf ? "（我）" : "");
            _chips.text = entry.Rank > 0 ? $"${entry.Chips:N0}" : "—";
            _background.color = entry.IsSelf ? new Color(0.19f, 0.30f, 0.29f, 1f) : new Color(0.08f, 0.13f, 0.16f, 0.94f);
        }
    }
}
