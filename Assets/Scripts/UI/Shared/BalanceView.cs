using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public enum BalanceCurrency { Dollars, Chips }

    public sealed class BalanceView : MonoBehaviour
    {
        [SerializeField] private BalanceCurrency _currency;
        [SerializeField] private bool _followAccount = true;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _amount;
        [SerializeField] private Sprite _dollarsIcon;
        [SerializeField] private Sprite _chipsIcon;
        private GameSettings _state;
        private uint? _shown;
        private bool _initialized;

        private void OnEnable()
        {
            _icon.sprite = _currency == BalanceCurrency.Dollars ? _dollarsIcon : _chipsIcon;
            _initialized = false;
            if (_followAccount)
            {
                _state = GameController.Available ? GameController.Instance.State : null;
                if (_state != null) _state.WalletChanged += Refresh;
                Refresh();
            }
        }
        private void OnDisable()
        {
            if (_state != null) _state.WalletChanged -= Refresh;
            _state = null;
        }
        private void Refresh() => Present(_state?.IsAuthenticated == true
            ? (_currency == BalanceCurrency.Dollars ? _state.Dollars : _state.Chips) : (uint?)null);
        public void Present(uint? amount)
        {
            if (_initialized && _shown == amount) return;
            _initialized = true;
            _shown = amount;
            _amount.text = amount.HasValue ? $"${amount.Value:N0}" : "—";
        }
    }
}
