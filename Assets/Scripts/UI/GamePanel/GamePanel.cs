using Framework.UI;
using Game.Network;
using Game.UI.GamePanelElement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class GamePanel : SingletonPanel<GamePanel>
    {
        [SerializeField]
        private TextMeshProUGUI _title;

        [SerializeField]
        private GameObject _playerPrefab;
        
        [SerializeField]
        private Transform _playerList;
        
        [SerializeField]
        private OperationArea _operationArea;

        private List<PlayerListItem> _playerListItems;

        private ClientGameInfo _info;
        public ClientGameInfo Info
        {
            get => _info;
            set
            {
                _info = value;
                Refresh();
            }
        }

        private void Refresh()
        {
            if(_playerListItems == null)
            {
                _playerListItems = new List<PlayerListItem>();
                for(int i = 0; i < _info.PlayersCount; i++)
                {
                    var obj = Instantiate(_playerPrefab, _playerList);
                    var component = obj.GetComponent<PlayerListItem>();
                    _playerListItems.Add(component);
                }
            }

            // 更新Player
            for(int i = 0; i < _info.PlayersCount; i++)
            {
                var listItem = _playerListItems[i];
                var playerInfo = _info.Players[i];
                listItem.IsDealer = i == _info.Dealer;
                listItem.IsSmallBlind = i == _info.SmallBlind;
                listItem.IsBigBlind = i == _info.BigBlind;
                listItem.IsOperating = i == _info.OperatingPlayer;
                listItem.IsFold = playerInfo.IsFold;
                listItem.IsDisconnected = playerInfo.IsDisconnected;
                listItem.PlayerName = playerInfo.PlayerName;
                listItem.Chip = playerInfo.Chips;
                listItem.ThinkingTime = _info.RoomInfo.ThinkingTime;
                listItem.Operation = playerInfo.LastOperation;
            }

            // 更新操作区
            _operationArea.Info = _info;

            // 更新标题
            _title.text = $"房间{_info.RoomInfo.Id}-{_info.Round}-总奖池${BigNumberToString(_info.TotalPot)}";
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
    }
}
