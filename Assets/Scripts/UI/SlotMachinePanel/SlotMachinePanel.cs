using System;
using Framework.UI;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class SlotMachinePanel : SingletonPanel<SlotMachinePanel>
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private Button _back;
        [SerializeField] private SlotMachineView _view;
        [SerializeField] private TextMeshProUGUI _price;
        [SerializeField] private TextMeshProUGUI _tenPrice;
        [SerializeField] private Button _singleDraw;
        [SerializeField] private Button _tenDraw;
        [SerializeField] private Button[] _prizeButtons;
        [SerializeField] private TextMeshProUGUI[] _probabilities;
        [SerializeField] private TextMeshProUGUI _noPrizeProbability;
        [SerializeField] private TextMeshProUGUI _pity;
        [SerializeField] private TextMeshProUGUI _status;
        private NetworkRoom _network;
        private SlotLotteryResponse _snapshot;
        private SlotDrawReceipt _receipt;
        private ulong _request;
        private SlotLotteryAction _action;
        private string _operation;
        private byte _drawCount = 1;
        private double _retryAt;
        private bool _hasSnapshot;

        private void OnEnable()
        {
            _back.onClick.AddListener(Back);
            _singleDraw.onClick.AddListener(Draw);
            _tenDraw.onClick.AddListener(DrawTen);
            _view.SpinRequested += Draw;
            _view.SpinFinished += ShowReward;
            _view.SetAvailability(false);
            _price.text = _tenPrice.text = "—";
            for (int i = 0; i < _prizeButtons.Length; i++)
            {
                var tier = (SlotPrizeTier)(i + 1);
                _prizeButtons[i].onClick.AddListener(() => OpenPool(tier));
                _prizeButtons[i].interactable = false;
                _probabilities[i].text = "—";
            }
            _noPrizeProbability.text = "";
            _pity.text = "";
            _network = NetworkManager.singleton as NetworkRoom;
            if (_network != null) _network.SlotLotteryReceived += Receive;
            FitContent();
            Send(SlotLotteryAction.Status);
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _singleDraw.onClick.RemoveListener(Draw);
            _tenDraw.onClick.RemoveListener(DrawTen);
            foreach (var button in _prizeButtons) button.onClick.RemoveAllListeners();
            _view.SpinRequested -= Draw;
            _view.SpinFinished -= ShowReward;
            _view.SetAvailability(false);
            if (_network != null) _network.SlotLotteryReceived -= Receive;
            _network = null;
            _request = 0;
            _receipt = null;
            _hasSnapshot = false;
        }

        private void Update()
        {
            if (_request != 0 && Time.realtimeSinceStartupAsDouble >= _retryAt)
            {
                _status.text = "正在确认抽奖结果…";
                Send(_action, _operation, _drawCount);
            }
        }

        private void Send(SlotLotteryAction action, string operation = null, byte count = 1)
        {
            _action = action;
            _operation = operation;
            _drawCount = count;
            _singleDraw.interactable = _tenDraw.interactable = false;
            _view.SetAvailability(false);
            foreach (var button in _prizeButtons) button.interactable = false;
            _request = _network?.RequestSlotLottery(action, operation, _snapshot.Revision, count: count) ?? 0;
            _retryAt = Time.realtimeSinceStartupAsDouble + 8d;
            _status.text = _request == 0 ? "请先登录" : action == SlotLotteryAction.Draw ? "正在抽奖…" : "正在读取…";
        }

        private void Draw() => Draw(1);
        private void DrawTen() => Draw(10);

        private void Draw(byte count)
        {
            if (_request != 0 || !_hasSnapshot || _receipt != null || !_snapshot.Enabled ||
                !CurrencySystem.CanAfford((ulong)_snapshot.CostDollars * count, _snapshot.Dollars, _snapshot.AvailableChips, _snapshot.CanExchange)) return;
            Send(SlotLotteryAction.Draw, Guid.NewGuid().ToString("N"), count);
        }

        private void Receive(SlotLotteryResponse response)
        {
            if (!isActiveAndEnabled || _request == 0 || response.RequestId != _request) return;
            if (response.Result == SlotLotteryResult.RateLimited)
            {
                _retryAt = Time.realtimeSinceStartupAsDouble + 0.3d;
                return;
            }
            _request = 0;
            _snapshot = response;
            _hasSnapshot = true;
            _price.text = $"${response.CostDollars:N0}";
            _tenPrice.text = $"${(ulong)response.CostDollars * 10:N0}";
            if (response.Disclosure != null)
            {
                for (int i = 0; i < _probabilities.Length; i++)
                    _probabilities[i].text = SlotProbabilityText.Format(response.Disclosure.Probability((SlotPrizeTier)(i + 1)));
                _noPrizeProbability.text = "未中奖 " + SlotProbabilityText.Format(response.Disclosure.Probability(SlotPrizeTier.None));
                // Wait until the result has been shown; an early reset would give the outcome away mid-spin.
                if (response.Receipt == null) _pity.text = PityText(response.Disclosure.DrawsUntilGuaranteed);
            }
            if (response.Result != SlotLotteryResult.Success)
            {
                RefreshAvailability();
                _status.text = response.Result switch
                {
                    SlotLotteryResult.InsufficientFunds => "余额不足",
                    SlotLotteryResult.ConfigurationChanged => "价格已更新，请重新抽奖",
                    SlotLotteryResult.RewardLimit => "奖励无法入账，本次未扣费",
                    SlotLotteryResult.RateLimited => "操作过于频繁，请稍后再试",
                    _ => "抽奖暂不可用"
                };
                return;
            }
            if (response.Receipt != null)
            {
                _receipt = response.Receipt;
                _status.text = "";
                _view.Present(_receipt.Tier, _receipt.DrawCount);
            }
            else
            {
                _receipt = null;
                RefreshAvailability();
            }
        }

        private static string PityText(int draws) => draws <= 0 ? "" : draws == 1 ? "下一抽必中奖" : $"{draws} 抽内必中奖";

        private void RefreshAvailability()
        {
            foreach (var button in _prizeButtons) button.interactable = _snapshot.Disclosure != null && _request == 0 && _receipt == null;
            bool affordable = CurrencySystem.CanAfford(_snapshot.CostDollars, _snapshot.Dollars,
                _snapshot.AvailableChips, _snapshot.CanExchange);
            bool ready = _hasSnapshot && _snapshot.Enabled && _receipt == null && _request == 0;
            _singleDraw.interactable = ready && affordable;
            _tenDraw.interactable = ready && CurrencySystem.CanAfford((ulong)_snapshot.CostDollars * 10,
                _snapshot.Dollars, _snapshot.AvailableChips, _snapshot.CanExchange);
            _view.SetAvailability(_singleDraw.interactable);
            _status.text = !_snapshot.Enabled ? "抽奖暂未开放" : !affordable ? "余额不足" :
                _snapshot.Dollars < _snapshot.CostDollars ? $"将自动兑换 {_snapshot.CostDollars - _snapshot.Dollars:N0} 筹码" : "";
        }

        private void OpenPool(SlotPrizeTier tier)
        {
            if (_request != 0 || _receipt != null || _snapshot.Disclosure == null) return;
            UIManager.Instance.PushPanel<SlotPrizePoolPanel>().Present(tier);
        }

        private void ShowReward()
        {
            if (_receipt == null || !isActiveAndEnabled) return;
            var results = _receipt.Results.Length > 0 ? _receipt.Results : new[]
            {
                new SlotDrawOutcome { Tier = _receipt.Tier, Reward = _receipt.Reward }
            };
            UIManager.Instance.PushPanel<SlotBatchResultPanel>().Present(results, Acknowledge);
        }

        private void Acknowledge()
        {
            if (!isActiveAndEnabled || _receipt == null) return;
            Send(SlotLotteryAction.Acknowledge, _receipt.OperationId);
        }

        private void OnRectTransformDimensionsChange() => FitContent();
        private void FitContent()
        {
            if (_content == null) return;
            var size = ((RectTransform)transform).rect.size;
            if (size.x > 0f && size.y > 0f)
                _content.localScale = Vector3.one * Mathf.Min(size.x / 1920f, size.y / 1080f);
        }
        private void Back() => UIManager.Instance.PopPanel<SlotMachinePanel>();
    }
}
