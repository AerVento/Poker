using Framework.SO;
using Framework.Timer;
using Game.Network;
using Game.UI;
using Game.UI.GamePanelElement;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class GamePanelLayoutTest
    {
        private GameObject _root;
        private GamePanel _panel;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("GamePanelLayoutTest", typeof(RectTransform), typeof(Canvas));
            _root.SetActive(false);
            if (!SingletonSOManager.Available)
            {
                var manager = new GameObject("CardResources").AddComponent<SingletonSOManager>();
                manager.transform.SetParent(_root.transform);
                typeof(SingletonSOManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, null);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/GamePanel/GamePanel.prefab");
            _panel = Object.Instantiate(prefab, _root.transform).GetComponent<GamePanel>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _root.GetComponentsInChildren<PlayerListItem>(true))
                item.IsOperating = false;
            Object.DestroyImmediate(_root);
        }

        [TestCase(0, 4, 1, 180f)]
        [TestCase(4, 4, 1, 180f)]
        [TestCase(5, 8, 2, 177f)]
        [TestCase(8, 8, 2, 177f)]
        [TestCase(9, 16, 2, 88.5f)]
        [TestCase(16, 16, 2, 88.5f)]
        public void LayoutUsesOccupiedSeatsAndPreservesServerIndices(int occupied, int tier, int columns, float height)
        {
            var info = Info(16);
            for (int i = 0; i < 16 - occupied; i++)
                info.Players[i] = default;
            info.CurrentPlayerIndex = -1;
            _panel.Info = info;
            var rows = Field<List<PlayerListItem>>(_panel, "_playerListItems");
            Assert.AreEqual(16, rows.Count);
            Assert.AreEqual(occupied, rows.Count(row => row.gameObject.activeSelf));
            Assert.IsTrue(rows.All(row => row.name == "Player" + tier + "(Clone)"));
            for (int i = 16 - occupied; i < 16; i++)
                Assert.AreEqual("Player" + i, rows[i].PlayerName);
            var grid = Field<PlayerListGrid>(_panel, "_playerGrid");
            Assert.AreEqual(columns, grid.constraintCount);
            Assert.AreEqual(height, grid.cellSize.y);
            var players = Field<RectTransform>(_panel, "_playerRegion");
            var table = Field<RectTransform>(_panel, "_tableRegion");
            Assert.Less(players.anchorMax.x, table.anchorMin.x, "The player and table regions must not overlap.");
            Assert.AreEqual(0.29385417f, players.anchorMax.x, 0.001f);
            grid.CalculateLayoutInputHorizontal();
            Assert.AreEqual(tier == 8 ? 4f / 3f : 8f / 3f, grid.cellSize.x / grid.cellSize.y, 0.0001f);
        }

        [Test]
        public void OnlyCrossingTierBoundaryReplacesInstancesInBothDirections()
        {
            _panel.Info = Info(5);
            var rows = Field<List<PlayerListItem>>(_panel, "_playerListItems");
            var old = rows[0];
            _panel.Info = Info(8);
            Assert.AreSame(old, rows[0]);
            _panel.Info = Info(9);
            Assert.IsTrue(old == null);
            old = rows[0];
            _panel.Info = Info(8);
            Assert.IsTrue(old == null);
            old = rows[0];
            _panel.Info = Info(4);
            Assert.IsTrue(old == null);
            Assert.AreEqual(4, rows.Count);
            _panel.Info = Info(2);
            Assert.AreEqual(2, rows.Count(row => row.gameObject.activeSelf));
        }

        [Test]
        public void CrossTierRebuildPreservesCountdownAndNewTurnRestoresFullDuration()
        {
            _panel.Info = Info(4);
            var rows = Field<List<PlayerListItem>>(_panel, "_playerListItems");
            var oldTimer = Field<Timer>(rows[0], "_timer");
            typeof(Timer).GetField("_time", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(oldTimer, 12d);
            _panel.Info = Info(5);
            Assert.IsFalse(oldTimer.IsCounting);
            Assert.AreEqual(18d, rows[0].CountdownRemaining);
            _panel.Info = Info(9);
            Assert.AreEqual(18d, rows[0].CountdownRemaining);
            var next = Info(4);
            next.TurnId++;
            _panel.Info = next;
            Assert.AreEqual(30d, rows[0].CountdownRemaining);
        }

        [Test]
        public void ExpiredCountdownDoesNotRestartOnCrossTierRebuild()
        {
            _panel.Info = Info(4);
            var rows = Field<List<PlayerListItem>>(_panel, "_playerListItems");
            Field<Timer>(rows[0], "_timer").Stop();
            _panel.Info = Info(5);
            Assert.IsTrue(rows[0].IsOperating);
            Assert.AreEqual(0, rows[0].CountdownRemaining);
            _panel.Info = Info(5);
            Assert.AreEqual(0, rows[0].CountdownRemaining);
        }

        [Test]
        public void HiddenOccupiedSeatStopsItsTimerWithoutChangingTier()
        {
            _panel.Info = Info(8);
            var rows = Field<List<PlayerListItem>>(_panel, "_playerListItems");
            var timer = Field<Timer>(rows[0], "_timer");
            var next = Info(8);
            next.Players[0] = default;
            _panel.Info = next;
            Assert.IsFalse(timer.IsCounting);
            Assert.IsFalse(rows[0].gameObject.activeSelf);
        }

        [TestCase(4)]
        [TestCase(8)]
        [TestCase(16)]
        public void SharedStatusHidesPreviousActionDuringTurnAndRestoresLatestResult(int count)
        {
            _panel.Info = Info(count);
            var row = Field<List<PlayerListItem>>(_panel, "_playerListItems")[0];
            var time = Field<TMPro.TextMeshProUGUI>(row, "_timeText");
            var icons = row.GetComponentsInChildren<OperationIcon>(true);
            var status = row.transform.Find("NamecardContent/StatusSlot");
            Assert.AreSame(status, time.transform.parent);
            Assert.AreSame(status, icons[0].transform.parent.parent);

            foreach (PlayerOperationType type in System.Enum.GetValues(typeof(PlayerOperationType)))
            {
                row.Operation = new PlayerOperation { Type = type, Number = 12345 };
                row.IsOperating = true;
                Assert.IsTrue(time.gameObject.activeSelf);
                Assert.IsFalse(icons.Any(icon => icon.gameObject.activeSelf));
                Field<Timer>(row, "_timer").Stop();
                row.IsOperating = true;
                Assert.IsTrue(time.gameObject.activeSelf, "A locally expired clock must wait for the server turn change.");
                Assert.IsFalse(icons.Any(icon => icon.gameObject.activeSelf));

                row.IsOperating = false;
                Assert.IsFalse(time.gameObject.activeSelf);
                var visible = icons.Where(icon => icon.gameObject.activeSelf).ToArray();
                Assert.AreEqual(1, visible.Length);
                Assert.AreEqual(12345u, visible[0].Chips);
            }
            row.Operation = null;
            Assert.IsFalse(time.gameObject.activeSelf);
            Assert.IsFalse(icons.Any(icon => icon.gameObject.activeSelf));

            row.Operation = new PlayerOperation { Type = PlayerOperationType.Call, Number = 200 };
            row.IsOperating = true;
            row.Operation = new PlayerOperation { Type = PlayerOperationType.Raise, Number = 800 };
            Assert.IsFalse(icons.Any(icon => icon.gameObject.activeSelf));
            row.IsOperating = false;
            Assert.IsTrue(Field<OperationIcon>(row, "_raiseIcon").gameObject.activeSelf);
            Assert.AreEqual(800u, Field<OperationIcon>(row, "_raiseIcon").Chips);
        }

        private static ClientGameInfo Info(int count)
        {
            return new ClientGameInfo
            {
                RoomInfo = new RoomInfo { Id = 42, ThinkingTime = 30 },
                HandId = "layout-hand", TurnId = 1,
                Players = Enumerable.Range(0, count).Select(i => new ClientGameInfo.PlayerInfo
                {
                    SeatIndex = i, IsOccupied = true, PlayerName = "Player" + i, Chips = 1000,
                }).ToList(),
                Hole = new List<Poker>(), Community = new List<Poker>(),
                CurrentPlayerIndex = 0, OperatingPlayer = 0,
                CurrentHighestPlayerBet = 100, CurrentHighestRaise = 100, CanRaise = true,
            };
        }

        private static T Field<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }
    }
}
