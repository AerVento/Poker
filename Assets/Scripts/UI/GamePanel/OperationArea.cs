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

        public ClientGameInfo Info
        {
            get => _info;
            set
            {
                _info = value;
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
            _betInput.onEndEdit.RemoveListener(OnBetOrRaiseChipsChanged);

            _checkButton.onClick.RemoveListener(OnCheck);
            _betOrRaiseButton.onClick.RemoveListener(OnBetOrRaise);
            _callButton.onClick.RemoveListener(OnCall);
            _allInButton.onClick.RemoveListener(OnAllIn);
            _foldButton.onClick.RemoveListener(OnFold);
        }

        private void OnBetOrRaiseChipsChanged(string value)
        {
            if(!uint.TryParse(value, out var num))
            {
                _betInput.text = _betChips.ToString();
                MsgBox.Create(msgbox =>
                {
                    msgbox.Title = "提示";
                    msgbox.Message = "请输入一个有效数字！";
                });
                return;
            }

            var availableTotal = (ulong)_info.CurrentPlayer.StageBet + _info.CurrentPlayer.Chips;
            var minimumRaiseTotal = (ulong)_info.CurrentHighestPlayerBet + _info.LeastRaise;
            if (!_info.CanRaise || num <= _info.CurrentHighestPlayerBet)
            {
                _betInput.text = minimumRaiseTotal.ToString();
                MsgBox.Create(msgbox =>
                {
                    msgbox.Title = "提示";
                    msgbox.Message = "当前行动没有加注权。";
                });
                return;
            }
            if (num > availableTotal)
            {
                _betInput.text = availableTotal.ToString();
                MsgBox.Create(msgbox =>
                {
                    msgbox.Title = "提示";
                    msgbox.Message = $"下注到${num}超出了你的可用总额${availableTotal}。";
                });
                return;
            }
            if (num < minimumRaiseTotal && num != availableTotal)
            {
                _betInput.text = minimumRaiseTotal.ToString();
                MsgBox.Create(msgbox =>
                {
                    msgbox.Title = "提示";
                    msgbox.Message = $"当前最少要加注到${minimumRaiseTotal}。\n只有 all-in 可以低于最小加注额。";
                });
                return;
            }
            _betChips = num;
        }

        private string BigNumberToString(uint number)
        {
            if (number < 1000)
                return number.ToString();
            else if (number < 1000000)
                return System.MathF.Round((float)number / 1000, 1).ToString() + "K";
            else if (number < 1000000000)
                return System.MathF.Round((float)number / 1000000, 1).ToString() + "M";
            else
                return System.MathF.Round((float)number / 1000000000, 1).ToString() + "B";
        }

        public void Refresh()
        {
            // 基本信息
            _playerNameText.text = _info.IsSpectator
                ? "旁观中"
                : _info.CurrentPlayer.PlayerName;
            _playerChipsText.text = _info.IsSpectator
                ? string.Empty
                : "$" + BigNumberToString(_info.CurrentPlayer.Chips);

            // 卡牌信息
            _hole.ForEach(poker => poker.IsShowing = false);
            _community.ForEach(poker => poker.IsShowing = false);
            for (int i = 0; i < _info.Hole.Count; i++)
            {
                _hole[i].IsShowing = true;
                _hole[i].Poker = _info.Hole[i];
            }
            for (int i = 0; i < _info.Community.Count; i++)
            {
                _community[i].IsShowing = true;
                _community[i].Poker = _info.Community[i];
            }

            if (_info.IsSpectator)
            {
                _checkButton.gameObject.SetActive(false);
                _betOrRaiseButton.gameObject.SetActive(false);
                _callButton.gameObject.SetActive(false);
                _allInButton.gameObject.SetActive(false);
                _foldButton.gameObject.SetActive(false);
                return;
            }


            // 如果不是你操作，则所有按钮都不该显示
            bool isOperating = _info.OperatingPlayer == _info.CurrentPlayerIndex;

            _checkButton.gameObject.SetActive(isOperating &&
                _info.CurrentPlayer.StageBet == _info.CurrentHighestPlayerBet);

            var availableTotal = (ulong)_info.CurrentPlayer.Chips + _info.CurrentPlayer.StageBet;
            var minimumRaiseTotal = (ulong)_info.CurrentHighestPlayerBet + _info.LeastRaise;
            _betOrRaiseButton.gameObject.SetActive(isOperating &&
                _info.CanRaise &&
                availableTotal > _info.CurrentHighestPlayerBet);
            _betOrRaiseTitle.text = _info.CurrentHighestPlayerBet == 0 ? "Bet" : "Raise";
            _betChips = (uint)Math.Min(availableTotal, minimumRaiseTotal);
            _betInput.text = _betChips.ToString();

            _callButton.gameObject.SetActive(isOperating && 
                _info.CurrentPlayer.StageBet < _info.CurrentHighestPlayerBet && 
                _info.CurrentPlayer.Chips > 0);

            _allInButton.gameObject.SetActive(isOperating &&
                (_info.CanRaise || availableTotal <= _info.CurrentHighestPlayerBet));

            _foldButton.gameObject.SetActive(isOperating);
        }

        private void OnCheck()
        {
            var req = new PlayerOperationRequest()
            {
                RoomId = _info.RoomInfo.Id,
                PlayerIndex = _info.CurrentPlayerIndex,
                Type = PlayerOperationType.Check,
            };
            NetworkClient.Send(req);
            Log.Write($"Send player operation request to the server: Type = {req.Type}");
        }

        private void OnBetOrRaise()
        {
            var req = new PlayerOperationRequest()
            {
                RoomId = _info.RoomInfo.Id,
                PlayerIndex = _info.CurrentPlayerIndex,
                Type = PlayerOperationType.Bet,
                Number = _betChips
            };
            NetworkClient.Send(req);
            Log.Write($"Send player operation request to the server: Type = {req.Type}, Num = {_betChips}");
        }

        private void OnCall()
        {
            var req = new PlayerOperationRequest()
            {
                RoomId = _info.RoomInfo.Id,
                PlayerIndex = _info.CurrentPlayerIndex,
                Type = PlayerOperationType.Call,
            };
            NetworkClient.Send(req);
            Log.Write($"Send player operation request to the server: Type = {req.Type}");
        }

        private void OnAllIn()
        {
            var req = new PlayerOperationRequest()
            {
                RoomId = _info.RoomInfo.Id,
                PlayerIndex = _info.CurrentPlayerIndex,
                Type = PlayerOperationType.AllIn,
            };
            NetworkClient.Send(req);
            Log.Write($"Send player operation request to the server: Type = {req.Type}");
        }

        private void OnFold()
        {
            var req = new PlayerOperationRequest()
            {
                RoomId = _info.RoomInfo.Id,
                PlayerIndex = _info.CurrentPlayerIndex,
                Type = PlayerOperationType.Fold,
            };
            NetworkClient.Send(req);
            Log.Write($"Send player operation request to the server: Type = {req.Type}");
        }
    }
}
