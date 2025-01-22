using Cysharp.Threading.Tasks;
using Framework.UI;
using Game.Network;
using Game.UI;
using Game.UI.GamePanelElement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Tests
{
    public class RoundResultMsgBoxTester : MonoBehaviour
    {
        private void Start()
        {
            UIManager.Instance.ShowPanel<RoundResultMsgBox>();
            Test();
        }
        private async void Test()
        {
            var panel = RoundResultMsgBox.Instance;
            while (true)
            {
                var deck = Poker.RandomDeck();
                var info = new RoundResult()
                {
                    Community = new List<Poker>() { deck.Dequeue(), deck.Dequeue(), deck.Dequeue(), deck.Dequeue() },
                    PlayerResults = new List<RoundResult.Player>()
                {
                    new RoundResult.Player()
                    {
                        PlayerName = "yurzhang",
                        IsFold = false,
                        Hole = new List<Poker>(){ deck.Dequeue(), deck.Dequeue()},
                        ChipEarned = 300,
                    },
                    new RoundResult.Player()
                    {
                        PlayerName = "kakie",
                        IsFold = true,
                        Hole = new List<Poker>(){ deck.Dequeue(), deck.Dequeue()},
                        ChipEarned = 0,
                    },
                    new RoundResult.Player()
                    {
                        PlayerName = "aervento",
                        IsFold = false,
                        Hole = new List<Poker>(){ deck.Dequeue(), deck.Dequeue()},
                        ChipEarned = 300,
                    },
                    new RoundResult.Player()
                    {
                        PlayerName = "schoparks",
                        IsFold = true,
                        Hole = new List<Poker>(){ deck.Dequeue(), deck.Dequeue() },
                        ChipEarned = 0,
                    }
                }
                };
                panel.Info = info;
                await UniTask.WaitForSeconds(10);
            }
            
        }
    }
}
