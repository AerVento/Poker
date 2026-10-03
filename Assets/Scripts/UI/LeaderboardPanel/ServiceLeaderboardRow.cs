using Game.Network.Accounts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class ServiceLeaderboardRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _rank;
        [SerializeField] private TextMeshProUGUI _name;
        [SerializeField] private TextMeshProUGUI _years;
        [SerializeField] private TextMeshProUGUI _devil;
        [SerializeField] private TextMeshProUGUI _archdevil;
        [SerializeField] private TextMeshProUGUI _satan;
        [SerializeField] private Image _background;
        [SerializeField] private Image _seal;

        private readonly InfernalNameStyle _nameStyle = new InfernalNameStyle();

        private void OnDestroy() => _nameStyle.Dispose();

        private static string ContractCount(ServiceLeaderboardEntry entry, int index) =>
            entry.Rank > 0 && entry.Contracts != null && index < entry.Contracts.Length ? entry.Contracts[index].ToString("N0") : "—";

        public void Show(ServiceLeaderboardEntry entry)
        {
            _nameStyle.Present(_name, entry.ServiceYears);
            _rank.text = entry.Rank > 0 ? entry.Rank.ToString() : "—";
            _name.text = entry.UserName == null ? "我的排名" : entry.UserName + (entry.IsSelf ? "（我）" : "");
            _years.text = entry.Rank > 0 ? $"{entry.ServiceYears:N0} <size=65%>年</size>" : "—";
            // The three columns show the first three contracts of Contracts.json.
            _devil.text = ContractCount(entry, 0);
            _archdevil.text = ContractCount(entry, 1);
            _satan.text = ContractCount(entry, 2);
            _background.color = entry.IsSelf ? new Color(0.30f, 0.09f, 0.08f) : new Color(0.13f, 0.065f, 0.075f);
            _seal.color = entry.Rank > 0 && entry.Rank <= 3
                ? new Color(0.65f, 0.14f, 0.12f) : new Color(0.24f, 0.10f, 0.12f);
        }
    }
}
