using Game.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Tests
{
    public class GameManagerTester : MonoBehaviour
    {
        private GameManager _manager = new GameManager();
        private void Start()
        {
            Test();
        }
        private void Test()
        {
            var roomInfo = new Network.RoomInfo()
            {
                Id = 5,
                Players = new List<string>()
                {
                    "AerVento", "yurzhang", "kakie", "kamui", "schoparks", "gyy"
                },
                OwnerName = "AerVento",
                MaxPlayerCount = 8,
                ReadyStatus = new List<bool>
                {
                    true, true, true, true, true, true
                },
                InitialChips = 1000,
                SmallBlindChips = 50,
                ThinkingTime = 20,
                IsWaiting = false
            };
            _manager.OnGameStageEnded += (id) => _manager.NextStage(id);
            _manager.OnRoomRoundEnded += (id, _) => _manager.StartNew(id);
            
            _manager.CreateGame(roomInfo);
            var id = roomInfo.Id;
            _manager.StartNew(id);

            var info = _manager.GetGameInfo(id);
            
            // 第一局： 950, 900, 700, 600, 400, 2450
            _manager.Bet(id, info.OperatingPlayer, 50);
            _manager.Bet(id, info.OperatingPlayer, 100);
            _manager.Bet(id, info.OperatingPlayer, 300);
            _manager.Bet(id, info.OperatingPlayer, 400);
            _manager.Bet(id, info.OperatingPlayer, 600);
            _manager.Bet(id, info.OperatingPlayer, 800);

            _manager.Fold(id, info.OperatingPlayer);
            _manager.Fold(id, info.OperatingPlayer);
            _manager.Fold(id, info.OperatingPlayer);
            _manager.Fold(id, info.OperatingPlayer);
            _manager.Fold(id, info.OperatingPlayer);

            // 第二局

        }
    }
}
