using Framework.Singleton;
using Framework.UI;
using Game.Network;
using Game.UI;
using Game.UI.GamePanelElement;
using Mirror;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public class UINavigationTest
    {
        private GameObject _root;
        private UIManager _ui;
        private GameController _controller;
        private NetworkRoom _network;
        private object _previousUI;
        private object _previousController;
        private object _previousConnection;
        private object _previousConnectionState;

        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo UIInstance = typeof(MonoSingleton<UIManager>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo ControllerInstance = typeof(MonoSingleton<GameController>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly PropertyInfo Connection = typeof(NetworkClient).GetProperty("connection");
        private static readonly FieldInfo ConnectionState = typeof(NetworkClient).GetField("connectState", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            _previousUI = UIInstance.GetValue(null);
            _previousController = ControllerInstance.GetValue(null);
            _previousConnection = Connection.GetValue(null);
            _previousConnectionState = ConnectionState.GetValue(null);
            _root = new GameObject("Navigation test");
            _root.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Manager/UIManager.prefab");
            _ui = Object.Instantiate(prefab, _root.transform).GetComponent<UIManager>();
            UIInstance.SetValue(null, _ui);
            foreach (var name in new[] { "_bottomLayer", "_middleLayer", "_topLayer", "_systemLayer" })
            {
                var layer = new GameObject(name, typeof(RectTransform));
                layer.transform.SetParent(_ui.transform);
                Set(_ui, name, layer.transform);
            }
            Invoke(_ui, "InitializeRuntimePanel");
            var basic = new GameObject("Basic panel", typeof(RectTransform), typeof(BasePanel));
            basic.transform.SetParent(_root.transform);
            Field<Dictionary<Type, GameObject>>(_ui, "_runtimePanels").Add(typeof(BasePanel), basic);
            _controller = new GameObject("Test account").AddComponent<GameController>();
            _controller.gameObject.SetActive(false);
            _controller.transform.SetParent(_root.transform);
            typeof(GameController).GetProperty("State").SetValue(_controller, new GameSettings());
            ControllerInstance.SetValue(null, _controller);
            _controller.State.SetAuthenticatedAccount("test", "Tester", 1000, null, "test-session");
            var networkObject = new GameObject("Test network");
            networkObject.SetActive(false);
            networkObject.transform.SetParent(_root.transform);
            _network = networkObject.AddComponent<NetworkRoom>();
            Connection.SetValue(null, new NetworkConnectionToServer { isAuthenticated = true });
            ConnectionState.SetValue(null, Enum.Parse(ConnectionState.FieldType, "Connected"));
        }

        [TearDown]
        public void TearDown()
        {
            _ui.ClearPanels();
            Object.DestroyImmediate(_root);
            UIInstance.SetValue(null, _previousUI);
            ControllerInstance.SetValue(null, _previousController);
            Connection.SetValue(null, _previousConnection);
            ConnectionState.SetValue(null, _previousConnectionState);
        }

        [Test]
        public void Push_KeepsCoveredPanelsActiveAndBlocksInputThroughThem()
        {
            var first = _ui.PushPanel<BasePanel>("A");
            var second = _ui.PushPanel<BasePanel>("B");
            Assert.IsTrue(first.gameObject.activeSelf);
            Assert.IsTrue(first.IsShowing);
            Assert.IsTrue(second.IsShowing);
            Assert.IsFalse(first.GetComponent<CanvasGroup>().interactable);
            Assert.IsFalse(first.GetComponent<CanvasGroup>().blocksRaycasts);
            Assert.IsTrue(second.GetComponent<CanvasGroup>().interactable);
            CollectionAssert.AreEqual(new[] { "A", "B" }, _ui.PanelOrder);
        }

        [Test]
        public void PopMiddle_PreservesTopAndReturnsToRemainingPredecessor()
        {
            var first = _ui.PushPanel<BasePanel>("A");
            var middle = _ui.PushPanel<BasePanel>("B");
            var top = _ui.PushPanel<BasePanel>("C");
            Assert.IsTrue(_ui.PopPanel("B"));
            Assert.IsFalse(middle.IsShowing);
            Assert.IsTrue(top.IsShowing);
            CollectionAssert.AreEqual(new[] { "A", "C" }, _ui.PanelOrder);
            Assert.IsTrue(_ui.PopPanel());
            Assert.AreEqual("A", _ui.TopPanelIdentifier);
            Assert.IsTrue(first.GetComponent<CanvasGroup>().interactable);
            Assert.IsFalse(top.IsShowing);
        }

        [Test]
        public void DuplicatePush_ReordersExistingInstanceWithoutDuplicatingHistory()
        {
            var first = _ui.PushPanel<BasePanel>("A");
            _ui.PushPanel<BasePanel>("B");
            Assert.AreSame(first, _ui.PushPanel<BasePanel>("A"));
            CollectionAssert.AreEqual(new[] { "B", "A" }, _ui.PanelOrder);
            Assert.Greater(first.transform.GetSiblingIndex(), _ui.GetPanel<BasePanel>("B").transform.GetSiblingIndex());
        }

        [Test]
        public void RenderLayers_KeepSystemDialogAboveNewMiddlePanel()
        {
            var dialog = _ui.PushPanel<BasePanel>("Dialog", UIManager.Layer.System);
            var page = _ui.PushPanel<BasePanel>("Page");
            Assert.AreEqual("Dialog", _ui.TopPanelIdentifier);
            Assert.IsTrue(dialog.GetComponent<CanvasGroup>().interactable);
            Assert.IsFalse(page.GetComponent<CanvasGroup>().interactable);
        }

        [Test]
        public void DirectHide_RemovesHistoryAndCanBeReopened()
        {
            var first = _ui.PushPanel<BasePanel>("A");
            var second = _ui.PushPanel<BasePanel>("B");
            second.Hide();
            Assert.AreEqual("A", _ui.TopPanelIdentifier);
            Assert.IsTrue(first.GetComponent<CanvasGroup>().interactable);
            second.Show();
            CollectionAssert.AreEqual(new[] { "A", "B" }, _ui.PanelOrder);
        }

        [Test]
        public void DestroyMiddle_RemovesHistoryWithoutClosingTop()
        {
            _ui.PushPanel<BasePanel>("A");
            _ui.PushPanel<BasePanel>("B");
            _ui.PushPanel<BasePanel>("C");
            _ui.DestroyPanel("B");
            CollectionAssert.AreEqual(new[] { "A", "C" }, _ui.PanelOrder);
            Assert.IsFalse(_ui.ShowingPanelIdentifiers.Contains("B"));
        }

        [Test]
        public void EmptyAndRepeatedPop_AreSafe()
        {
            Assert.IsFalse(_ui.PopPanel());
            Assert.IsFalse(_ui.PopPanel("missing"));
            _ui.PushPanel<BasePanel>("A");
            Assert.IsTrue(_ui.PopPanel());
            Assert.IsFalse(_ui.PopPanel("A"));
            Assert.IsNull(_ui.TopPanelIdentifier);
        }

        [Test]
        public void InGameSettings_UsesDedicatedPanelAndBackRestoresLiveTable()
        {
            var game = _ui.PushPanel<GamePanel>();
            Invoke(game, "OpenSettings");
            Assert.AreEqual(nameof(InGameOptionPanel), _ui.TopPanelIdentifier);
            Assert.IsTrue(game.IsShowing);
            Assert.IsFalse(game.GetComponent<CanvasGroup>().interactable);
            Assert.IsFalse(_ui.ContainsPanel<OptionPanel>());
            var settings = _ui.GetPanel<InGameOptionPanel>();
            Assert.IsNull(settings.transform.Find("Buttons/LogoutButton"));
            Assert.IsNull(settings.transform.Find("TableActions"));
            Assert.NotNull(game.transform.Find("TableActions/LeaveButton"));
            Assert.IsNull(game.transform.Find("LeaveButton"));
            Invoke(settings, "Back");
            Assert.AreEqual(nameof(GamePanel), _ui.TopPanelIdentifier);
            Assert.IsTrue(game.GetComponent<CanvasGroup>().interactable);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SettingsBack_ReturnsToActualOriginRegardlessOfCachedAccount(bool fromLobby)
        {
            var title = _ui.PushPanel<TitlePanel>();
            if (fromLobby)
                _ui.PushPanel<RoomListPanel>();
            var settings = _ui.PushPanel<OptionPanel>();
            Invoke(settings, "Back");
            Assert.AreEqual(fromLobby ? nameof(RoomListPanel) : nameof(TitlePanel), _ui.TopPanelIdentifier);
            Assert.IsTrue(title.IsShowing);
            Assert.IsFalse(settings.IsShowing);
        }

        [Test]
        public void RemovingSettingsOrigin_DoesNotReopenRemovedLobby()
        {
            _ui.PushPanel<TitlePanel>();
            _ui.PushPanel<RoomListPanel>();
            var settings = _ui.PushPanel<OptionPanel>();
            _ui.PopPanel<RoomListPanel>();
            Invoke(settings, "Back");
            CollectionAssert.AreEqual(new[] { nameof(TitlePanel) }, _ui.PanelOrder);
        }

        [Test]
        public void ClosingLegacyResult_DoesNotNavigateOrLeaveRoom()
        {
            _controller.State.SetLastRoom(42);
            var room = _ui.PushPanel<RoomPanel>();
            var result = _ui.PushPanel<GameResultMsgBox>(UIManager.Layer.Top);
            result.Hide();
            Assert.AreEqual(nameof(RoomPanel), _ui.TopPanelIdentifier);
            Assert.IsTrue(room.IsShowing);
            Assert.AreEqual(42, _controller.State.LastRoomId);
            Assert.IsFalse(_ui.ContainsPanel<TitlePanel>());
        }

        [Test]
        public void Reset_DismissesModalWithoutInvokingConfirmationAndRetainsOnlyNewRoot()
        {
            _ui.PushPanel<RoomPanel>();
            bool confirmed = false;
            var box = MsgBox.Create("Test modal");
            box.OnConfirm += () => confirmed = true;
            _ui.ResetPanels<TitlePanel>();
            CollectionAssert.AreEqual(new[] { nameof(TitlePanel) }, _ui.PanelOrder);
            Assert.IsFalse(confirmed);
            Assert.IsFalse(_ui.ShowingPanelIdentifiers.Contains("Test modal"));
        }

        [Test]
        public void DisconnectFromSettings_ClearsInvalidReturnHistoryImmediately()
        {
            _ui.PushPanel<TitlePanel>();
            _ui.PushPanel<RoomListPanel>();
            var settings = _ui.PushPanel<OptionPanel>();
            Set(_network, "_isConnected", true);
            Set(_network, "_silentDisconnect", true);
            _network.OnClientDisconnect();
            Invoke(settings, "Back");
            CollectionAssert.AreEqual(new[] { nameof(TitlePanel) }, _ui.PanelOrder);
        }

        [TestCase(false, true, 42)]
        [TestCase(true, false, 42)]
        [TestCase(true, true, 99)]
        public void RoomSync_RejectsDisconnectedNonmemberAndUnrelatedRoom(bool connected, bool member, int roomId)
        {
            _ui.PushPanel<TitlePanel>();
            Set(_network, "_activeClientRoomId", (int?)42);
            _controller.State.SetLastRoom(42);
            if (!connected)
                ConnectionState.SetValue(null, Enum.Parse(ConnectionState.FieldType, "Disconnected"));
            var room = CreateRoom(roomId, member ? "Tester" : "Other");
            Invoke(_network, "RoomInfoSyncHandler", new RoomInfoSync { RoomInfo = room });
            Assert.AreEqual(nameof(TitlePanel), _ui.TopPanelIdentifier);
            Assert.AreEqual(42, _controller.State.LastRoomId);
        }

        [Test]
        public void StaleJoinAndStartResponses_DoNotOpenRoomOrGame()
        {
            _ui.PushPanel<TitlePanel>();
            Invoke(_network, "PlayerJoinRoomResponseHandler", new PlayerJoinRoomResponse
            {
                SuccessCode = 0, RoomInfo = CreateRoom(42, "Tester"),
            });
            Invoke(_network, "PlayerStartGameResponseHandler", new PlayerStartGameResponse { SuccessCode = 0 });
            CollectionAssert.AreEqual(new[] { nameof(TitlePanel) }, _ui.PanelOrder);
        }

        [Test]
        public void MatchingRoomRefresh_DoesNotCloseSettingsOrReorderIt()
        {
            var panel = _ui.PushPanel<RoomPanel>();
            Invoke(panel, "Awake");
            _ui.PushPanel<OptionPanel>();
            Set(_network, "_activeClientRoomId", (int?)42);
            _controller.State.SetLastRoom(42);
            Invoke(_network, "RoomInfoSyncHandler", new RoomInfoSync { RoomInfo = CreateRoom(42, "Tester") });
            Assert.AreEqual(nameof(OptionPanel), _ui.TopPanelIdentifier);
            Assert.AreEqual(42, panel.RoomInfo.Id);
        }

        [Test]
        public void DailyTasksPrefabHasCompleteEntryCardsAndNavigationReferences()
        {
            var title = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/TitlePanel.prefab").GetComponent<TitlePanel>();
            Assert.IsNotNull(Field<UnityEngine.UI.Button>(title, "_tasks"));
            var panel = _ui.PushPanel<DailyTaskPanel>();
            foreach (var name in new[] { "_back", "_refresh" })
                Assert.IsNotNull(Field<UnityEngine.UI.Button>(panel, name));
            var cards = Field<DailyTaskCard[]>(panel, "_cards");
            Assert.AreEqual(2, cards.Length);
            Assert.AreEqual(Game.Network.Accounts.DailyTaskId.DailySupply, cards[0].TaskId);
            Assert.AreEqual(Game.Network.Accounts.DailyTaskId.Comeback, cards[1].TaskId);
            foreach (var card in cards)
            {
                Assert.IsNotNull(Field<UnityEngine.UI.Button>(card, "_claim"));
                foreach (var name in new[] { "_title", "_description", "_reward", "_progress", "_status", "_claimLabel" })
                    Assert.IsNotNull(Field<TMPro.TextMeshProUGUI>(card, name));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TaskLoginReturnsToTaskPanelUnlessRoomRecoveryHasPriority(bool hasRoom)
        {
            _ui.PushPanel<TitlePanel>();
            var login = _ui.PushPanel<LoginPanel>();
            Invoke(login, "Awake");
            Set(login, "<DestinationAfterLogin>k__BackingField", Enum.Parse(typeof(LoginPanel).GetProperty("DestinationAfterLogin", Private).PropertyType, "DailyTasks"));
            Invoke(_network, "HandleClientAuthentication", new Game.Network.AccountAuthenticationResponse
            {
                Code = Game.Network.Accounts.AccountAuthenticationCode.Success,
                HasLastRoom = hasRoom,
                LastRoomId = 42,
            });
            Assert.AreEqual(hasRoom ? nameof(RoomListPanel) : nameof(DailyTaskPanel), _ui.TopPanelIdentifier);
            Assert.IsFalse(_ui.ContainsPanel<LoginPanel>());
        }

        [Test]
        public void CancellingTaskLoginDoesNotRedirectLaterNormalLogin()
        {
            var login = _ui.PushPanel<LoginPanel>();
            Set(login, "<DestinationAfterLogin>k__BackingField", Enum.Parse(typeof(LoginPanel).GetProperty("DestinationAfterLogin", Private).PropertyType, "DailyTasks"));
            Invoke(login, "OnDisable");
            Assert.AreEqual("Lobby", typeof(LoginPanel).GetProperty("DestinationAfterLogin", Private).GetValue(login).ToString());
        }

        [Test]
        public void TasksBackAndDisconnectPreserveTitleDestination()
        {
            _ui.PushPanel<TitlePanel>();
            var tasks = _ui.PushPanel<DailyTaskPanel>();
            Invoke(tasks, "Back");
            Assert.AreEqual(nameof(TitlePanel), _ui.TopPanelIdentifier);
            _ui.PushPanel<DailyTaskPanel>();
            Set(_network, "_silentDisconnect", true);
            Set(_network, "_isConnected", true);
            _network.OnClientDisconnect();
            CollectionAssert.AreEqual(new[] { nameof(TitlePanel) }, _ui.PanelOrder);
        }

        [TestCase("pending")]
        [TestCase("active")]
        [TestCase("last-room")]
        public void TasksCannotOpenDuringRoomEntryOrFromInsideRoom(string state)
        {
            Assert.IsTrue(_network.CanOpenDailyTasks);
            if (state == "pending") Set(_network, "_roomEntryPending", true);
            if (state == "active") Set(_network, "_activeClientRoomId", (int?)42);
            if (state == "last-room") _controller.State.SetLastRoom(42);
            Assert.IsFalse(_network.CanOpenDailyTasks);
            Assert.AreEqual(0, _network.RequestDailyTasks());
            Assert.AreEqual(0, _network.RequestDailyTaskClaim(Game.Network.Accounts.DailyTaskId.DailySupply, 20260905));
        }

        [Test]
        public void ReopeningLoginImmediatelyRestoresControlsAfterPreviousPendingRequest()
        {
            var login = _ui.PushPanel<LoginPanel>();
            Field<UnityEngine.UI.Button>(login, "_login").interactable = false;
            Field<UnityEngine.UI.Button>(login, "_register").interactable = false;
            Invoke(login, "OnEnable");
            Assert.IsTrue(Field<UnityEngine.UI.Button>(login, "_login").interactable);
            Assert.IsTrue(Field<UnityEngine.UI.Button>(login, "_register").interactable);
            Invoke(login, "OnDisable");
        }

        [Test]
        public void TaskResponsesIgnoreEarlierOpenAndClosedPanel()
        {
            _ui.PushPanel<TitlePanel>();
            var panel = _ui.PushPanel<DailyTaskPanel>();
            // Give this panel an active hierarchy while retaining the fixture's isolated account.
            panel.transform.SetParent(null);
            try
            {
                Set(panel, "_statusRequest", (ulong)20);
                var response = new DailyTasksResponse
                {
                    RequestId = 19,
                    Result = Game.Network.Accounts.DailyTaskResult.Success,
                    Snapshot = new Game.Network.Accounts.DailyTasksSnapshot
                    {
                        Balance = 2000, Period = 20260905, ServerUnixSeconds = 100, NextResetUnixSeconds = 200,
                        Tasks = new[]
                        {
                            new Game.Network.Accounts.DailyTaskState { Id = Game.Network.Accounts.DailyTaskId.DailySupply,
                                Name = "每日补给", Reward = 1000, Availability = Game.Network.Accounts.DailyTaskAvailability.Claimed },
                            new Game.Network.Accounts.DailyTaskState { Id = Game.Network.Accounts.DailyTaskId.Comeback,
                                Name = "东山再起", Reward = 2000, Availability = Game.Network.Accounts.DailyTaskAvailability.BalanceTooHigh },
                        },
                    },
                };
                panel.ReceiveStatus(response);
                Assert.AreEqual(1000, _controller.State.Chips);
                Assert.AreEqual(20, Field<ulong>(panel, "_statusRequest"));
                response.RequestId = 20;
                panel.ReceiveStatus(response);
                Assert.AreEqual(2000, _controller.State.Chips);
                Assert.IsTrue(Field<bool>(panel, "_hasSnapshot"));
                Set(panel, "_claimRequest", (ulong)22);
                panel.ReceiveClaim(new DailyTaskClaimResponse { RequestId = 21, Result = Game.Network.Accounts.DailyTaskResult.Success });
                Assert.AreEqual(22, Field<ulong>(panel, "_claimRequest"));
                _ui.PopPanel<DailyTaskPanel>();
                Set(panel, "_statusRequest", (ulong)20);
                response.Snapshot.Balance = 5000;
                panel.ReceiveStatus(response);
                Assert.AreEqual(2000, _controller.State.Chips);
                Assert.AreEqual(nameof(TitlePanel), _ui.TopPanelIdentifier);
            }
            finally { panel.transform.SetParent(_root.transform); }
        }

        [Test]
        public void AccountFeaturesExistOnlyOnTitle()
        {
            var title = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/TitlePanel.prefab").GetComponent<TitlePanel>();
            foreach (var field in new[] { "_shop", "_leaderboard" })
                Assert.IsNotNull(Field<UnityEngine.UI.Button>(title, field));
            var lobby = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/RoomListPanel/RoomListPanel.prefab");
            Assert.IsNull(lobby.transform.Find("ShopButton"));
            Assert.IsNull(lobby.transform.Find("LeaderboardButton"));
        }

        [TestCase("Shop", false)]
        [TestCase("Shop", true)]
        [TestCase("Collection", false)]
        [TestCase("Collection", true)]
        [TestCase("Leaderboard", false)]
        [TestCase("Leaderboard", true)]
        public void FeatureLoginRoutesToSelectionButRoomRecoveryTakesPriority(string destination, bool hasRoom)
        {
            _ui.PushPanel<TitlePanel>();
            var login = _ui.PushPanel<LoginPanel>();
            Invoke(login, "Awake");
            SetLoginDestination(login, destination);
            Invoke(_network, "HandleClientAuthentication", new AccountAuthenticationResponse
            {
                Code = Game.Network.Accounts.AccountAuthenticationCode.Success,
                HasLastRoom = hasRoom,
                LastRoomId = 42,
            });
            Assert.AreEqual(hasRoom ? nameof(RoomListPanel) : destination == "Collection" ? nameof(ShopPanel) : destination + "Panel", _ui.TopPanelIdentifier);
            if (!hasRoom && destination == "Collection")
                Assert.AreEqual(ShopPageMode.Collection, _ui.GetPanel<ShopPanel>().Mode);
            Assert.IsFalse(_ui.ContainsPanel<LoginPanel>());
        }

        [TestCase("Shop")]
        [TestCase("Collection")]
        [TestCase("Leaderboard")]
        public void CancelledFeatureLoginCannotRedirectNextNormalLogin(string destination)
        {
            var login = _ui.PushPanel<LoginPanel>();
            SetLoginDestination(login, destination);
            Invoke(login, "OnDisable");
            Assert.AreEqual("Lobby", typeof(LoginPanel).GetProperty("DestinationAfterLogin", Private).GetValue(login).ToString());
            Invoke(_network, "HandleClientAuthentication", new AccountAuthenticationResponse
            {
                Code = Game.Network.Accounts.AccountAuthenticationCode.Success,
            });
            Assert.AreEqual(nameof(RoomListPanel), _ui.TopPanelIdentifier);
        }

        [TestCase("Shop")]
        [TestCase("Collection")]
        [TestCase("Leaderboard")]
        public void FeatureBackAndDisconnectReturnToTitle(string destination)
        {
            _ui.PushPanel<TitlePanel>();
            BasePanel panel = destination != "Leaderboard" ? (BasePanel)_ui.PushPanel<ShopPanel>() : _ui.PushPanel<LeaderboardPanel>();
            if (destination == "Collection") ((ShopPanel)panel).Open(ShopPageMode.Collection);
            Invoke(panel, "Back");
            Assert.AreEqual(nameof(TitlePanel), _ui.TopPanelIdentifier);
            if (destination != "Leaderboard") _ui.PushPanel<ShopPanel>().Open(
                destination == "Collection" ? ShopPageMode.Collection : ShopPageMode.Shop);
            else _ui.PushPanel<LeaderboardPanel>();
            Set(_network, "_silentDisconnect", true);
            Set(_network, "_isConnected", true);
            _network.OnClientDisconnect();
            CollectionAssert.AreEqual(new[] { nameof(TitlePanel) }, _ui.PanelOrder);
        }

        [TestCase("Collection", "pending")]
        [TestCase("Collection", "active")]
        [TestCase("Collection", "last-room")]
        [TestCase("Shop", "pending")]
        [TestCase("Shop", "active")]
        [TestCase("Shop", "last-room")]
        [TestCase("Leaderboard", "pending")]
        [TestCase("Leaderboard", "active")]
        [TestCase("Leaderboard", "last-room")]
        public void TitleFeatureCannotBypassRoomRestrictions(string destination, string state)
        {
            var singleton = typeof(NetworkManager).GetProperty("singleton");
            var previous = singleton.GetValue(null);
            try
            {
                singleton.SetValue(null, _network);
                var title = _ui.PushPanel<TitlePanel>();
                if (state == "pending") Set(_network, "_roomEntryPending", true);
                if (state == "active") Set(_network, "_activeClientRoomId", (int?)42);
                if (state == "last-room") _controller.State.SetLastRoom(42);
                Invoke(title, destination);
                Assert.AreEqual(nameof(TitlePanel), _ui.TopPanelIdentifier);
            }
            finally { singleton.SetValue(null, previous); }
        }

        [TestCase("Shop")]
        [TestCase("Collection")]
        [TestCase("Leaderboard")]
        public void UnauthenticatedTitleEntryRemembersLoginDestination(string destination)
        {
            var singleton = typeof(NetworkManager).GetProperty("singleton");
            var previous = singleton.GetValue(null);
            try
            {
                singleton.SetValue(null, _network);
                ((NetworkConnectionToServer)Connection.GetValue(null)).isAuthenticated = false;
                _controller.State.ClearAuthenticatedAccount();
                var title = _ui.PushPanel<TitlePanel>();
                Invoke(title, destination);
                Assert.AreEqual(nameof(LoginPanel), _ui.TopPanelIdentifier);
                var login = _ui.GetPanel<LoginPanel>();
                Assert.AreEqual(destination, typeof(LoginPanel).GetProperty("DestinationAfterLogin", Private).GetValue(login).ToString());
            }
            finally { singleton.SetValue(null, previous); }
        }

        private static void SetLoginDestination(LoginPanel login, string destination)
        {
            var type = typeof(LoginPanel).GetProperty("DestinationAfterLogin", Private).PropertyType;
            Set(login, "<DestinationAfterLogin>k__BackingField", Enum.Parse(type, destination));
        }

        private static RoomInfo CreateRoom(int id, string owner)
        {
            var room = RoomInfo.CreateRoom(owner);
            room.Id = id;
            return room;
        }

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    }
}
