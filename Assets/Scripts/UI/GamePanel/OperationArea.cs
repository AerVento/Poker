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
            else if (num < _info.CurrentHighestPlayerBet + _info.LeastRaise)
            {
                _betInput.text = (_info.CurrentHighestPlayerBet + _info.LeastRaise).ToString();
                MsgBox.Create(msgbox =>
                {
                    msgbox.Title = "提示";
                    msgbox.Message = $"当前最少要加注到${_info.CurrentHighestPlayerBet + _info.LeastRaise}。\n你加注到${num}，达不到最小加注金额。";
                });
                return;
            }
            else if(_info.CurrentHighestPlayerBet - _info.CurrentPlayer.StageBet + num >= _info.CurrentPlayer.Chips)
            {
                _betInput.text = (_info.CurrentPlayer.Chips - 1).ToString();
                MsgBox.Create(msgbox =>
                {
                    msgbox.Title = "提示";
                    msgbox.Message = $"此次加注需要消耗${_info.CurrentHighestPlayerBet - _info.CurrentPlayer.StageBet + num}，而你只有${_info.CurrentPlayer.Chips}。";
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
            _playerNameText.text = _info.CurrentPlayer.PlayerName;
            _playerChipsText.text = "$" + BigNumberToString(_info.CurrentPlayer.Chips);

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


            // 如果不是你操作，则所有按钮都不该显示
            bool isOperating = _info.OperatingPlayer == _info.CurrentPlayerIndex;

            // 如果前方之前所有人都是Check，则可以Check
            // 但有例外：在Preflop阶段，除了大盲以外的人都Check的话，那么大盲还有一次权力
            bool exception = _info.Round == GameStage.Preflop && _info.BigBlind == _info.CurrentPlayerIndex && _info.CurrentHighestPlayerBet == _info.CurrentPlayer.StageBet;
            _checkButton.gameObject.SetActive(isOperating && (exception || _info.CurrentHighestPlayerBet == 0));

            // 如果当前筹码不够最小加价，则不能Bet/Raise
            // 能够Bet/Raise，一定能够Call
            _betOrRaiseButton.gameObject.SetActive(isOperating && _info.CurrentPlayer.Chips + _info.CurrentPlayer.StageBet > _info.CurrentHighestPlayerBet + _info.LeastRaise);
            _betOrRaiseTitle.text = _info.CurrentHighestPlayerBet == 0 ? "Bet" : "Raise";
            // 修改Bet/Raise的最小值
            _betChips = _info.CurrentHighestPlayerBet + _info.LeastRaise;
            _betInput.text = _betChips.ToString();

            // 如果你投入的筹码和最多的人一样多，那就不能Call（因为没必要）
            // 如果推入全部筹码也无法与最多的人持平，则不能Call
            _callButton.gameObject.SetActive(isOperating && 
                _info.CurrentPlayer.StageBet < _info.CurrentHighestPlayerBet && 
                _info.CurrentPlayer.Chips + _info.CurrentPlayer.StageBet > _info.CurrentHighestPlayerBet);

            // 什么时候都可以AllIn
            _allInButton.gameObject.SetActive(isOperating);

            // Preflop阶段，如果其他人没有加价，则大盲不能Fold，其余情况可以Fold
            _foldButton.gameObject.SetActive(isOperating &&
                !(
                    _info.Round == GameStage.Preflop && 
                    _info.BigBlind == _info.CurrentPlayerIndex &&
                    _info.CurrentHighestPlayerBet == _info.CurrentPlayer.StageBet
                    ));;
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
