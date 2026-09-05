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
