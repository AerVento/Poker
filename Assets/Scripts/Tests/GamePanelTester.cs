using Cysharp.Threading.Tasks;
using Framework.UI;
using Game.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Tests
{
    public class GamePanelTester : MonoBehaviour
    {
        private void Start()
        {
            UIManager.Instance.ShowPanel<GamePanel>();
            //Test();
        }
        //private async void Test()
        //{
        //    //var panel = GamePanel.Instance;
        //    //var roomInfo = new Network.RoomInfo()
        //    //{
        //    //    Id = 5,
        //    //    Players = new List<string>()
        //    //    {
        //    //        "AerVento", "yurzhang", "kakie", "kamui", "schoparks"
        //    //    },
        //    //    OwnerName = "AerVento",
        //    //    MaxPlayerCount = 8,
        //    //    ReadyStatus = new List<bool>
        //    //    {
        //    //        true, true, true, true, true
        //    //    },
        //    //    InitialChips = 500000000,
        //    //    SmallBlindChips = 1,
        //    //    ThinkingTime = 20,
        //    //    IsWaiting = false
        //    //};
        //    //var serverGameInfo = Network.ServerGameInfo.Create(roomInfo);
        //    //var deck = Poker.RandomDeck();
        //    //foreach(var player in serverGameInfo.Players)
        //    //{
        //    //    player.Hole.Add(deck.Dequeue());
        //    //    player.Hole.Add(deck.Dequeue());
        //    //}
        //    //var clientGameInfo = new Network.ClientGameInfo(serverGameInfo, 2);
        //    //panel.Info = clientGameInfo;

        //    //await UniTask.WaitForSeconds(5); 


        //    //// 1
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].Chips -= 100000;
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].LastBet += 100000;
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].LastOperation = new Network.PlayerOperation()
        //    //{
        //    //    Type = Network.PlayerOperationType.Bet,
        //    //    Number = 100000,
        //    //};
        //    //serverGameInfo.OperatingPlayer = (serverGameInfo.OperatingPlayer + 1) % serverGameInfo.PlayersCount;
        //    //clientGameInfo = new Network.ClientGameInfo(serverGameInfo, 2);
        //    //panel.Info = clientGameInfo;
        //    //await UniTask.WaitForSeconds(1);


        //    ////2
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].Chips -= 100000;
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].LastBet += 100000;
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].LastOperation = new Network.PlayerOperation()
        //    //{
        //    //    Type = Network.PlayerOperationType.Call,
        //    //    Number = 100000,
        //    //};
        //    //serverGameInfo.OperatingPlayer = (serverGameInfo.OperatingPlayer + 1) % serverGameInfo.PlayersCount;
        //    //clientGameInfo = new Network.ClientGameInfo(serverGameInfo, 2);
        //    //panel.Info = clientGameInfo;
        //    //await UniTask.WaitForSeconds(1);


        //    //// 3
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].Chips -= 100000;
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].LastBet += 100000;
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].LastOperation = new Network.PlayerOperation()
        //    //{
        //    //    Type = Network.PlayerOperationType.Call,
        //    //    Number = 100000,
        //    //};
        //    //serverGameInfo.OperatingPlayer = (serverGameInfo.OperatingPlayer + 1) % serverGameInfo.PlayersCount;
        //    //clientGameInfo = new Network.ClientGameInfo(serverGameInfo, 2);
        //    //panel.Info = clientGameInfo;
        //    //await UniTask.WaitForSeconds(1);

        //    //// 4
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].Chips -= 100000;
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].LastBet += 100000;
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].LastOperation = new Network.PlayerOperation()
        //    //{
        //    //    Type = Network.PlayerOperationType.Call,
        //    //    Number = 100000,
        //    //};
        //    //serverGameInfo.OperatingPlayer = (serverGameInfo.OperatingPlayer + 1) % serverGameInfo.PlayersCount;
        //    //clientGameInfo = new Network.ClientGameInfo(serverGameInfo, 2);
        //    //panel.Info = clientGameInfo;
        //    //await UniTask.WaitForSeconds(1);


        //    //// 5
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].IsFold = true;
        //    //serverGameInfo.Players[clientGameInfo.OperatingPlayer].LastOperation = new Network.PlayerOperation()
        //    //{
        //    //    Type = Network.PlayerOperationType.Fold,
        //    //};
        //    //if(serverGameInfo.OperatingPlayer == serverGameInfo.BigBlind)
        //    //{
        //    //    serverGameInfo.BigBlind = (serverGameInfo.BigBlind + 1) % serverGameInfo.PlayersCount;
        //    //}
        //    //if (serverGameInfo.OperatingPlayer == serverGameInfo.SmallBlind)
        //    //{
        //    //    serverGameInfo.SmallBlind = (serverGameInfo.SmallBlind + 1) % serverGameInfo.PlayersCount;
        //    //}
        //    //serverGameInfo.OperatingPlayer = (serverGameInfo.OperatingPlayer + 1) % serverGameInfo.PlayersCount;
        //    //clientGameInfo = new Network.ClientGameInfo(serverGameInfo, 2);
        //    //panel.Info = clientGameInfo;
        //    //await UniTask.WaitForSeconds(1);

        //    //serverGameInfo.Community.Add(deck.Dequeue());
        //    //serverGameInfo.Community.Add(deck.Dequeue());
        //    //serverGameInfo.Community.Add(deck.Dequeue());
        //    //clientGameInfo = new Network.ClientGameInfo(serverGameInfo, 2);
        //    //panel.Info = clientGameInfo;
        //    //await UniTask.WaitForSeconds(1);
        //}
    }
}
