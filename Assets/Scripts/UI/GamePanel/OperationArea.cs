using Framework.Log;
using Game.Network;
using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.GamePanelElement
{
    public class OperationArea : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField]
        private Button _checkButton;
        
        [SerializeField]
        private Button _betOrRaiseButton;

        [SerializeField]
        private TextMeshProUGUI _betOrRaiseTitle;

        [SerializeField]
        private TMP_InputField _betInput;

        [SerializeField]
        private TextMeshProUGUI _betHint;

        [SerializeField]
        private Button _callButton;

        [SerializeField]
        private Button _allInButton;

        [SerializeField]
        private Button _foldButton;

        [Header("UI Label")]
        [SerializeField]
        private TextMeshProUGUI _playerNameText;
        [SerializeField]
        private TextMeshProUGUI _playerChipsText;

        [Header("Pokers")]
        [SerializeField]
        private List<PokerElement> _hole;
        [SerializeField]
        private List<PokerElement> _community;

        private ClientGameInfo _info;

        private uint _betChips;
        private bool _operationPending;
        private bool _resetBetInput = true;
        private MsgBox _confirmation;
        private int _confirmationVersion;

        private bool CanOperate => _info.RoomInfo != null && !_info.IsSpectator &&
            _info.Round < GameStage.RoundOver &&
            _info.OperatingPlayer == _info.CurrentPlayerIndex &&
            _info.CurrentPlayer.IsOccupied && !_info.CurrentPlayer.IsFold &&
            !_info.CurrentPlayer.IsAllIn && !_info.CurrentPlayer.IsDisconnected &&
            !_info.CurrentPlayer.IsLeaving && !_info.CurrentPlayer.IsWaitingForNextHand;

        private ulong AvailableTotal => Math.Min(uint.MaxValue,
            (ulong)_info.CurrentPlayer.StageBet + _info.CurrentPlayer.Chips);

        private uint MinimumBetTotal => (uint)Math.Min(AvailableTotal,
            (ulong)_info.CurrentHighestPlayerBet + _info.LeastRaise);

        public ClientGameInfo Info
        {
            get => _info;
            set
            {
                _resetBetInput = _info.RoomInfo == null || value.RoomInfo == null ||
                    _info.RoomInfo.Id != value.RoomInfo.Id || _info.Round != value.Round ||
                    _info.HandId != value.HandId || _info.TurnId != value.TurnId ||
                    _info.OperatingPlayer != value.OperatingPlayer ||
                    _info.CurrentPlayerIndex != value.CurrentPlayerIndex ||
                    _info.CurrentHighestPlayerBet != value.CurrentHighestPlayerBet ||
                    _info.LeastRaise != value.LeastRaise || _info.CanRaise != value.CanRaise ||
                    _info.CurrentPlayer.Chips != value.CurrentPlayer.Chips ||
                    _info.CurrentPlayer.StageBet != value.CurrentPlayer.StageBet;
                _info = value;
                if (_resetBetInput || !CanOperate)
                {
                    CloseConfirmation();
                    SetBetHint(string.Empty);
                }
                _operationPending = false;
                Refresh();
            }
        }

        private void OnEnable()
        {
            _betInput.onEndEdit.AddListener(OnBetOrRaiseChipsChanged);

            _checkButton.onClick.AddListener(OnCheck);
            _betOrRaiseButton.onClick.AddListener(OnBetOrRaise);
            _callButton.onClick.AddListener(OnCall);
            _allInButton.onClick.AddListener(OnAllIn);
            _foldButton.onClick.AddListener(OnFold);
        }

        private void OnDisable()
        {
            CloseConfirmation();
            SetBetHint(string.Empty);
            _betInput.onEndEdit.RemoveListener(OnBetOrRaiseChipsChanged);

            _checkButton.onClick.RemoveListener(OnCheck);
            _betOrRaiseButton.onClick.RemoveListener(OnBetOrRaise);
            _callButton.onClick.RemoveListener(OnCall);
            _allInButton.onClick.RemoveListener(OnAllIn);
            _foldButton.onClick.RemoveListener(OnFold);
        }

        private void OnBetOrRaiseChipsChanged(string value)
        {
            if (!CanOperate || !_info.CanRaise || _operationPending)
                return;

            var valid = ulong.TryParse(value, out var amount);
            if (!valid)
                amount = _betChips;
            var minimum = MinimumBetTotal;
            var maximum = AvailableTotal;
            var corrected = (uint)Math.Max(minimum, Math.Min(maximum, amount));
            SetBetHint(!valid ? "请输入有效的非负整数，已恢复金额。" :
                amount < minimum ? $"已调整至最低可下注总额 ${ChipAmountFormatter.Format(minimum)}。" :
                amount > maximum ? $"已调整至可用总额 ${ChipAmountFormatter.Format(checked((uint)maximum))}（全下）。" : string.Empty);
            _betChips = corrected;
            _betInput.SetTextWithoutNotify(_betChips.ToString());
        }

        private void SetBetHint(string message)
        {
            if (_betHint != null)
            {
                _betHint.text = message;
                _betHint.gameObject.SetActive(!string.IsNullOrEmpty(message));
            }
        }

        public void OnOperationRejected()
        {
            _operationPending = false;
            Refresh();
        }



        public void Refresh()
        {
            if (_info.RoomInfo == null)
                return;

            // 基本信息
            _playerNameText.text = _info.IsSpectator
                ? "旁观中"
                : _info.CurrentPlayer.PlayerName;
            _playerChipsText.text = _info.IsSpectator
                ? string.Empty
                : "$" + ChipAmountFormatter.Format(_info.CurrentPlayer.Chips);

            // 卡牌信息
            _hole.ForEach(poker => poker.IsShowing = false);
            _community.ForEach(poker => poker.IsShowing = false);
            for (int i = 0; i < (_info.Hole?.Count ?? 0) && i < _hole.Count; i++)
            {
                _hole[i].IsShowing = true;
                _hole[i].Poker = _info.Hole[i];
            }
            for (int i = 0; i < (_info.Community?.Count ?? 0) && i < _community.Count; i++)
            {
                _community[i].IsShowing = true;
                _community[i].Poker = _info.Community[i];
            }

            bool isOperating = CanOperate;
            var availableTotal = AvailableTotal;
            bool canBet = isOperating && _info.CanRaise &&
                availableTotal > _info.CurrentHighestPlayerBet;

            _checkButton.gameObject.SetActive(isOperating &&
                _info.CurrentPlayer.StageBet == _info.CurrentHighestPlayerBet);
            _betOrRaiseButton.gameObject.SetActive(canBet);
            _betInput.gameObject.SetActive(canBet);
            _betOrRaiseTitle.text = _info.CurrentHighestPlayerBet == 0 ? "下注到" : "加注到";
            if (_resetBetInput)
            {
                _betChips = MinimumBetTotal;
                _betInput.SetTextWithoutNotify(_betChips.ToString());
                _resetBetInput = false;
            }

            _callButton.gameObject.SetActive(isOperating &&
                _info.CurrentPlayer.StageBet < _info.CurrentHighestPlayerBet &&
                _info.CurrentPlayer.Chips > 0);
            _allInButton.gameObject.SetActive(isOperating && _info.CurrentPlayer.Chips > 0 &&
                (_info.CanRaise || availableTotal <= _info.CurrentHighestPlayerBet));
            _foldButton.gameObject.SetActive(isOperating);

            _checkButton.interactable = !_operationPending;
            _betOrRaiseButton.interactable = !_operationPending;
            _betInput.interactable = !_operationPending;
            _callButton.interactable = !_operationPending;
            _allInButton.interactable = !_operationPending;
            _foldButton.interactable = !_operationPending;
        }

        private void OnCheck() => SendOperation(PlayerOperationType.Check);

        private void OnBetOrRaise()
        {
            if (!_betOrRaiseButton.isActiveAndEnabled || !_betOrRaiseButton.interactable)
                return;
            OnBetOrRaiseChipsChanged(_betInput.text);
            RequestOperation(PlayerOperationType.Bet, _betChips);
        }

        private void OnCall() => RequestOperation(PlayerOperationType.Call);

        private void OnAllIn() => RequestOperation(PlayerOperationType.AllIn);

        private void OnFold() => RequestOperation(PlayerOperationType.Fold);

        private bool UsesAllRemainingChips(PlayerOperationType type, uint number)
        {
            var player = _info.CurrentPlayer;
            return player.Chips > 0 && (type == PlayerOperationType.AllIn ||
                type == PlayerOperationType.Call && _info.CurrentHighestPlayerBet > player.StageBet &&
                    (ulong)_info.CurrentHighestPlayerBet - player.StageBet >= player.Chips ||
                type == PlayerOperationType.Bet && (ulong)number >= (ulong)player.StageBet + player.Chips);
        }

        private void RequestOperation(PlayerOperationType type, uint number = 0)
        {
            if (!CanOperate || _operationPending || _confirmation != null)
                return;
            var allIn = UsesAllRemainingChips(type, number);
            var freeFold = type == PlayerOperationType.Fold &&
                _info.CurrentPlayer.StageBet == _info.CurrentHighestPlayerBet;
            if (!allIn && !freeFold)
            {
                SendOperation(type, number);
                return;
            }

            var version = _confirmationVersion;
            var box = MsgBox.Create("PokerOperation");
            _confirmation = box;
            box.Type = MsgboxType.ConfirmAndCancel;
            box.Title = allIn ? "确认全下" : "确认弃牌";
            box.Message = allIn
                ? $"确定投入剩余全部 ${ChipAmountFormatter.Format(_info.CurrentPlayer.Chips)} 筹码？\n确认期间，操作倒计时仍会继续。"
                : "当前可以免费过牌，确定弃牌？\n确认期间，操作倒计时仍会继续。";
            box.ConfirmButtonText = allIn ? "确认全下" : "确认弃牌";
            box.CancelButtonText = "取消";
            box.PreferCancel = true;
            box.OnCancel += () => _confirmation = null;
            box.OnConfirm += () =>
            {
                _confirmation = null;
                if (isActiveAndEnabled && version == _confirmationVersion && CanOperate)
                    SendOperation(type, number);
            };
        }

        private void CloseConfirmation()
        {
            _confirmationVersion++;
            var box = _confirmation;
            _confirmation = null;
            if (box != null)
                box.Hide();
        }

        private void SendOperation(PlayerOperationType type, uint number = 0)
        {
            if (!CanOperate || _operationPending || !NetworkClient.isConnected ||
                NetworkClient.connection == null || !NetworkClient.connection.isAuthenticated)
                return;

            _operationPending = true;
            Refresh();
            var request = new PlayerOperationRequest
            {
                RoomId = _info.RoomInfo.Id,
                HandId = _info.HandId,
                TurnId = _info.TurnId,
                PlayerIndex = _info.CurrentPlayerIndex,
                Type = type,
                Number = number,
            };
            NetworkClient.Send(request);
            GameAudioManager.EnsureClient()?.PlayOperation(type, _info, number);
            Log.Write($"Send player operation request to the server: Type = {type}, Num = {number}");
        }
    }
}
