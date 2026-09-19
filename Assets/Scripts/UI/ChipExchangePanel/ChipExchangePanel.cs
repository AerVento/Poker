using Framework.UI;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class ChipExchangePanel : SingletonPanel<ChipExchangePanel>
    {
        private enum ExchangeDirection
        {
            DollarsToChips,
            ChipsToDollars,
        }

        [Header("Navigation")]
        [SerializeField] private Button _back;
        [SerializeField] private Button _refresh;

        [Header("Balances")]
        [SerializeField] private TextMeshProUGUI _dollars;
        [SerializeField] private TextMeshProUGUI _chips;
        [SerializeField] private TextMeshProUGUI _availableChips;

        [Header("Direction")]
        [SerializeField] private Button _dollarsToChips;
        [SerializeField] private Button _chipsToDollars;
        [SerializeField] private GameObject _dollarsToChipsSelected;
        [SerializeField] private GameObject _chipsToDollarsSelected;

        [Header("Exchange")]
        [SerializeField] private TMP_InputField _amount;
        [SerializeField] private Button _all;
        [SerializeField] private Button _exchange;
        [SerializeField] private TextMeshProUGUI _exchangeLabel;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private GameObject _busyIndicator;

        private NetworkRoom _networkRoom;
        private ExchangeDirection _direction;
        private ulong _pendingRequestId;
        private uint _dollarBalance;
        private uint _chipBalance;
        private uint _availableChipBalance;
        private bool _canExchange;

        private bool IsBusy => _pendingRequestId != 0;

        private void OnEnable()
        {
            _back.onClick.AddListener(Back);
            _refresh.onClick.AddListener(RequestRefresh);
            _dollarsToChips.onClick.AddListener(SelectDollarsToChips);
            _chipsToDollars.onClick.AddListener(SelectChipsToDollars);
            _all.onClick.AddListener(SetMaximumAmount);
            _exchange.onClick.AddListener(Exchange);
            _amount.onValueChanged.AddListener(HandleAmountChanged);
            _amount.contentType = TMP_InputField.ContentType.IntegerNumber;
            _amount.characterLimit = 10;
            ResolveNetworkRoom();
            Subscribe();
            SetDirection(ExchangeDirection.DollarsToChips);
            SetSnapshot(
                GameController.Available ? GameController.Instance.State.Dollars : 0,
                GameController.Available ? GameController.Instance.State.Chips : 0,
                GameController.Available ? GameController.Instance.State.Chips : 0,
                false);
            RequestRefresh();
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _refresh.onClick.RemoveListener(RequestRefresh);
            _dollarsToChips.onClick.RemoveListener(SelectDollarsToChips);
            _chipsToDollars.onClick.RemoveListener(SelectChipsToDollars);
            _all.onClick.RemoveListener(SetMaximumAmount);
            _exchange.onClick.RemoveListener(Exchange);
            _amount.onValueChanged.RemoveListener(HandleAmountChanged);
            Unsubscribe();
            _pendingRequestId = 0;
            _amount.text = string.Empty;
        }

        private void Back() => UIManager.Instance.PopPanel(Identifier);

        private void ResolveNetworkRoom()
        {
            if (_networkRoom == null)
                _networkRoom = NetworkManager.singleton as NetworkRoom;
        }

        private void Subscribe()
        {
            if (_networkRoom != null)
                _networkRoom.WalletReceived += HandleWalletResponse;
        }

        private void Unsubscribe()
        {
            if (_networkRoom != null)
                _networkRoom.WalletReceived -= HandleWalletResponse;
        }

        private void RequestRefresh()
        {
            if (IsBusy)
                return;
            ResolveNetworkRoom();
            if (_networkRoom == null || !_networkRoom.CanOpenWallet)
            {
                SetStatus("兑换服务暂不可用。");
                RefreshControls();
                return;
            }

            _pendingRequestId = _networkRoom.RequestWallet();
            if (_pendingRequestId == 0)
            {
                SetStatus("兑换服务暂不可用。");
                RefreshControls();
                return;
            }
            SetStatus("正在刷新……");
            RefreshControls();
        }

        private void SelectDollarsToChips() => SetDirection(ExchangeDirection.DollarsToChips);

        private void SelectChipsToDollars() => SetDirection(ExchangeDirection.ChipsToDollars);

        private void SetDirection(ExchangeDirection direction)
        {
            _direction = direction;
            bool toChips = direction == ExchangeDirection.DollarsToChips;
            _dollarsToChipsSelected.SetActive(toChips);
            _chipsToDollarsSelected.SetActive(!toChips);
            _exchangeLabel.text = toChips ? "兑换筹码" : "兑换美金";
            _amount.text = string.Empty;
            SetStatus(string.Empty);
            RefreshControls();
        }

        private void SetMaximumAmount()
        {
            uint maximum = _direction == ExchangeDirection.DollarsToChips
                ? _dollarBalance
                : _availableChipBalance;
            _amount.text = maximum.ToString();
        }

        private void HandleAmountChanged(string _) => RefreshControls();

        private void Exchange()
        {
            if (IsBusy || !_canExchange)
                return;
            if (!uint.TryParse(_amount.text, out uint amount) || amount == 0)
            {
                SetStatus("请输入有效金额。");
                return;
            }

            uint maximum = _direction == ExchangeDirection.DollarsToChips
                ? _dollarBalance
                : _availableChipBalance;
            if (amount > maximum)
            {
                SetStatus(_direction == ExchangeDirection.DollarsToChips
                    ? "美金不足。"
                    : "可兑换筹码不足。");
                return;
            }

            ResolveNetworkRoom();
            bool dollarsToChips = _direction == ExchangeDirection.DollarsToChips;
            _pendingRequestId = _networkRoom == null
                ? 0
                : _networkRoom.ExchangeWallet(dollarsToChips, amount);
            if (_pendingRequestId == 0)
            {
                SetStatus("兑换服务暂不可用。");
                RefreshControls();
                return;
            }

            SetStatus("正在兑换……");
            RefreshControls();
        }

        private void HandleWalletResponse(WalletResponse response)
        {
            if (response.RequestId != _pendingRequestId)
                return;

            _pendingRequestId = 0;
            if (response.HasSnapshot)
            {
                SetSnapshot(response.Dollars, response.Chips, response.AvailableChips, response.CanExchange);
                if (GameController.Available)
                    GameController.Instance.State.SetWallet(response.Dollars, response.Chips);
            }

            if (response.Result == WalletResult.Success || response.Result == WalletResult.AlreadyApplied)
            {
                SetStatus(response.IsExchange
                    ? "兑换成功。"
                    : response.CanExchange ? string.Empty : "本手进行中暂不可兑换。");
                if (response.IsExchange)
                    _amount.text = string.Empty;
            }
            else
                SetStatus(GetErrorMessage(response.Result));
            RefreshControls();
        }

        private void SetSnapshot(uint dollars, uint chips, uint availableChips, bool canExchange)
        {
            _dollarBalance = dollars;
            _chipBalance = chips;
            _availableChipBalance = availableChips;
            _canExchange = canExchange;
            _dollars.text = $"${dollars:N0}";
            _chips.text = $"${chips:N0}";
            _availableChips.text = availableChips == chips
                ? string.Empty
                : $"可兑换 ${availableChips:N0}";
            RefreshControls();
        }

        private void RefreshControls()
        {
            uint maximum = _direction == ExchangeDirection.DollarsToChips
                ? _dollarBalance
                : _availableChipBalance;
            bool validAmount = uint.TryParse(_amount.text, out uint amount) && amount > 0 && amount <= maximum;
            RefreshDirectionButton(_dollarsToChips, _direction == ExchangeDirection.DollarsToChips);
            RefreshDirectionButton(_chipsToDollars, _direction == ExchangeDirection.ChipsToDollars);
            _amount.interactable = !IsBusy && _canExchange;
            _all.interactable = !IsBusy && _canExchange && maximum > 0;
            _exchange.interactable = !IsBusy && _canExchange && validAmount;
            _refresh.interactable = !IsBusy;
            _back.interactable = true;
            if (_busyIndicator != null)
                _busyIndicator.SetActive(IsBusy);
        }

        private void RefreshDirectionButton(Button button, bool selected)
        {
            var colors = button.colors;
            colors.disabledColor = selected
                ? new Color(0.075f, 0.19f, 0.17f, 1f)
                : new Color(0.43f, 0.46f, 0.43f, 1f);
            button.colors = colors;
            button.interactable = !IsBusy && !selected;

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.color = selected
                    ? new Color(0.94f, 0.92f, 0.84f, 1f)
                    : new Color(0.12f, 0.16f, 0.15f, 1f);
        }

        private void SetStatus(string message)
        {
            if (_status != null)
                _status.text = message;
        }

        private static string GetErrorMessage(WalletResult result)
        {
            return result switch
            {
                WalletResult.InvalidRequest => "兑换请求无效。",
                WalletResult.Unavailable => "兑换服务暂不可用。",
                WalletResult.ExchangeLocked => "本手进行中暂不可兑换。",
                WalletResult.InsufficientFunds => "余额不足。",
                WalletResult.BalanceLimit => "兑换后余额超出上限。",
                WalletResult.ConversionRequired => "需要先完成货币兑换。",
                WalletResult.QuoteChanged => "余额已变化，请重试。",
                WalletResult.RateLimited => "操作过快，请稍后重试。",
                _ => "兑换失败，请重试。",
            };
        }
    }
}
