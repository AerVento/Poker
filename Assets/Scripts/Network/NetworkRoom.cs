using Assets.Scripts.Network.Messages;
using Cysharp.Threading.Tasks;
using Framework.Log;
using Framework.UI;
using Game.UI;
using Game.UI.GamePanelElement;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using UnityEngine;

namespace Game.Network
{
    public class NetworkRoom : NetworkManager
    {
        #region Server Only
        // server only
        private RoomManager _roomManager = new RoomManager();
        private PlayerManager _playerManager = new PlayerManager();
        private GameManager _gameManager = new GameManager();

        #endregion

        #region Client Only
        // Client Only
        /// <summary>
        /// 标识当前是否连接到服务器（客户端使用）
        /// 不使用<see cref="NetworkClient.isConnected"/>的原因是因为调用<see cref="NetworkManager.OnClientDisconnect"/>时就已经将其值设为false。这将使得无从知晓是否曾经正常连接过。
        /// </summary>
        private bool _isConnected = false;

        /// <summary>
        /// "正在连接服务器"的MsgBox
        /// </summary>
        private MsgBox _connectingMsgbox;

        #endregion

        public override void Awake()
        {
            base.Awake();
            _gameManager.OnGameInfoChanged += OnGameInfoChanged;
            _gameManager.OnGameStageEnded += OnGameStageEnd;
            _gameManager.OnRoomRoundEnded += OnRoomRoundEnd;
            _gameManager.OnRoomGameOver += OnGameOver;
        }

        #region Server
        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.RegisterHandler<PlayerChangeNameMessage>(PlayerChangeNameMessageHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<PlayerChangeReadyMessage>(PlayerChangeReadyMessageHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<PlayerConnectMessage>(PlayerConnectMessageHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<PlayerCreateRoomRequest>(PlayerCreateRoomRequestHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<PlayerJoinRoomRequest>(PlayerJoinRoomRequestHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<PlayerLeaveRoomRequest>(PlayerLeaveRoomRequestHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<PlayerOperationRequest>(PlayerOperationRequestHandler, requireAuthentication:false);
            NetworkServer.RegisterHandler<PlayerStartGameRequest>(PlayerStartGameRequestHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<RoomInfoChange>(RoomInfoChangeHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<RoomListRequest>(RoomListRequestHandler, requireAuthentication: false);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // base.OnServerDisconnect(conn);
            if (_playerManager.RemoveConnection(conn, out var name))
            {

                Log.WriteInfo($"Player {name} from {conn.address} is disconnected.");
                if (_roomManager.WhichRoom(name, out var roomId))
                {
                    var room = _roomManager.FindRoom(roomId);
                    // 还在大厅中等待
                    if (room.IsWaiting)
                    {
                        int result = _roomManager.Leave(roomId, name);
                        switch (result)
                        {
                            case 0:
                                var sync = new RoomInfoSync() { RoomInfo = room };
                                foreach (var player in room.Players)
                                {
                                    var playerConn = _playerManager.GetConnection(player);
                                    playerConn?.Send(sync);
                                }
                                break;
                        }
                    }
                    else // 游戏已经开始
                    {
                        _gameManager.SetDisconnected(roomId, name);   
                    }
                }
            }
        }
        #endregion Server

        #region Client
        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<GameInfoSync>(GameInfoSyncHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<GameResultNotification>(GameResultNotificationHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<PlayerJoinRoomResponse>(PlayerJoinRoomResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<PlayerOperationResponse>(PlayerOperationResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<PlayerStartGameResponse>(PlayerStartGameResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<RoomInfoSync>(RoomInfoSyncHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<RoomListResponse>(RoomListResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<RoundResultNotification>(RoundResultNotificationHandler, requireAuthentication: false);
            
            MsgBox.Create("ConnectingServer", (setting) =>
            {
                _connectingMsgbox = setting;
                setting.Type = MsgboxType.Cancel;
                setting.Title = "通知";
                setting.Message = "正在连接到服务器";
                setting.ShowCloseButton = false;
                setting.OnCancel += () =>
                {
                    StopClient();
                };
            });
        }

        public override void OnClientConnect()
        {
            // base.OnClientConnect();

            // 销毁连接时打开的Msgbox"ConnectingServer"
            if (_connectingMsgbox != null)
            {
                _connectingMsgbox.Hide();
                _connectingMsgbox = null;
            }

            _isConnected = true;

            UIManager.Instance.HidePanel<TitlePanel>();
            UIManager.Instance.ShowPanel<RoomListPanel>();
            var message = new PlayerConnectMessage();
            message.PlayerName = GameController.Instance.State.Nickname;
            NetworkClient.Send(message);
        }

        public override void OnClientError(TransportError error, string reason)
        {
            // base.OnClientError(error, reason);
            MsgBox.Create((msgbox) =>
            {
                msgbox.Title = "无法连接至服务器";
                msgbox.Message = $"{error}:{reason}";
            });
        }

        public override void OnClientTransportException(Exception exception)
        {
            // base.OnClientTransportException(exception);
            MsgBox.Create((msgbox) =>
            {
                msgbox.Title = "无法连接至服务器";
                msgbox.Message = $"{exception}";
            });
        }

        public override void OnClientDisconnect()
        {
            // base.OnClientDisconnect();

            // 销毁连接时打开的Msgbox
            if (_connectingMsgbox != null)
            {
                _connectingMsgbox.Hide();
                _connectingMsgbox = null;
            }
            bool oldConnectedStatus = _isConnected;
            _isConnected = false;

            foreach (var identifier in UIManager.Instance.ShowingPanelIdentifiers.ToList())
            {
                UIManager.Instance.DestroyPanel(identifier);
            }
            UniTask.Void(async () =>
            {
                await UniTask.NextFrame();

                // 等待一帧后再打开TitlePanel
                // 若不等待，同一帧中Destroy并不会真的Destory，若此时出现一个新的TitlePanel，则会导致Singleton冲突
                if (UIManager.Available)
                {
                    UIManager.Instance.ShowPanel<TitlePanel>();
                    MsgBox.Create((msgbox) =>
                    {

                        if (oldConnectedStatus)
                        {
                            msgbox.Title = "与服务器断开连接";
                            msgbox.Message = "有可能网络状态差，请检查网络连接\n也有极小可能是有同名玩家进入，请考虑更换昵称";
                        }
                        else
                        {
                            msgbox.Title = "无法连接至服务器";
                            msgbox.Message = "请检查网络连接";
                        }
                    });
                }
            });


        }

        #endregion

        #region Message Handler

        private void GameInfoSyncHandler(GameInfoSync message)
        {
            if (GamePanel.Instance == null)
                UIManager.Instance.ShowPanel<GamePanel>();
            GamePanel.Instance.Info = message.Info;
            Log.WriteInfo("Game Sync:" + JsonSerializer.Serialize(message.Info, new JsonSerializerOptions() { WriteIndented = true }));
        }

        private void GameResultNotificationHandler(GameResultNotification message)
        {
            UIManager.Instance.ShowPanel<GameResultMsgBox>(UIManager.Layer.Top);
            GameResultMsgBox.Instance.Info = message.GameResult;
        }

        private void PlayerChangeNameMessageHandler(NetworkConnectionToClient conn, PlayerChangeNameMessage message)
        {
            Log.Write($"Player {message.OldName} from {conn.address} requested to changed its player name to {message.NewName}.");
            if (_playerManager.IsConnected(message.NewName))
            {
                Log.Write($"The new name {message.NewName} is occupied by another online player, so the connection will be shutdown.");
                conn.Disconnect();
                return;
            }
            _playerManager.RemoveConnection(message.OldName);
            _playerManager.SetConnection(message.NewName, conn);
            Log.Write($"Player {message.OldName} from {conn.address} successfully changed its player name to {message.NewName}.");
        }

        private void PlayerChangeReadyMessageHandler(NetworkConnectionToClient conn, PlayerChangeReadyMessage message)
        {
            int result = _roomManager.PlayerChangeReady(message.RoomId, message.PlayerName);
            if(result == 0)
            {
                var room = _roomManager.FindRoom(message.RoomId);
                var sync = new RoomInfoSync() { RoomInfo = room };
                foreach(var player in room.Players)
                {
                    var playerConn = _playerManager.GetConnection(player);
                    playerConn?.Send(sync);
                }
            }
        }
        
        private void PlayerConnectMessageHandler(NetworkConnectionToClient conn, PlayerConnectMessage message)
        {
            Log.Write($"Player {message.PlayerName} from {conn.address} is connecting to the server.");
            if (_playerManager.IsConnected(message.PlayerName))
            {
                Log.Write($"Player {message.PlayerName} from {conn.address} is already connected to the server. Therefore the cuurent connection will be disconnected.");
                conn.Disconnect();
            }
            else
            {
                _playerManager.SetConnection(message.PlayerName, conn);
                Log.Write($"Player {message.PlayerName} from {conn.address} is connected to the server.");
            }
        }

        private void PlayerCreateRoomRequestHandler(NetworkConnectionToClient conn, PlayerCreateRoomRequest message)
        {
            var room = _roomManager.CreateRoom(message.PlayerName);
            var sync = new RoomInfoSync() { RoomInfo = room };
            conn.Send(sync);
        }

        private void PlayerJoinRoomRequestHandler(NetworkConnectionToClient conn, PlayerJoinRoomRequest request)
        {
            int result = _roomManager.Join(request.RoomId, request.PlayerName);
            var response = new PlayerJoinRoomResponse()
            {
                SuccessCode = result,
            };
            if (result == 0)
            {
                var room = _roomManager.FindRoom(request.RoomId);
                response.RoomInfo = room;
                conn.Send(response);

                var sync = new RoomInfoSync() { RoomInfo = room };
                foreach (var player in room.Players)
                {
                    if (player == request.PlayerName)
                        continue; // 如果是刚加入房间的人，就不给他发Sync了，response里面会给他提供room信息，自己更新就可以
                    var playerConn = _playerManager.GetConnection(player);
                    playerConn?.Send(sync);
                }
            }
            else
                conn.Send(response);
        }

        private void PlayerJoinRoomResponseHandler(PlayerJoinRoomResponse response)
        {
            int result = response.SuccessCode;
            switch (result)
            {
                case 0:
                    UIManager.Instance.HidePanel<RoomListPanel>();
                    UIManager.Instance.ShowPanel<RoomPanel>();
                    RoomPanel.Instance.RoomInfo = response.RoomInfo;
                    return;
                case -1:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "加入房间时出错";
                        msgbox.Message = "选定的房间不存在";
                    });
                    RoomListPanel.Instance.RefreshRoomList();
                    return;
                case -2:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "加入房间时出错";
                        msgbox.Message = $"玩家{GameController.Instance.State.Nickname}已经在房间中";
                    });
                    return;
                case -3:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "加入房间时出错";
                        msgbox.Message = "房间已满";
                    });
                    RoomListPanel.Instance.RefreshRoomList();
                    return;
                case -4:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "加入房间时出错";
                        msgbox.Message = "游戏已开始";
                    });
                    RoomListPanel.Instance.RefreshRoomList();
                    return;
                default:
                    Log.WriteError($"Unknown success code: {result}.");
                    Log.WriteError(new System.Diagnostics.StackTrace().ToString());
                    return;
            }
        }

        private void PlayerLeaveRoomRequestHandler(NetworkConnectionToClient conn, PlayerLeaveRoomRequest message)
        {
            int result = _roomManager.Leave(message.RoomId, message.PlayerName);
            switch (result)
            {
                case 0:
                    var room = _roomManager.FindRoom(message.RoomId);
                    if(room != null)
                    {
                        var sync = new RoomInfoSync() { RoomInfo = room };
                        foreach (var player in room.Players)
                        {
                            var playerConn = _playerManager.GetConnection(player);
                            playerConn?.Send(sync);
                        }
                    }
                    break;
                case -1:
                    Log.WriteError($"Player {message.PlayerName} requested to leave the room {message.RoomId}, which is not exist. This shouldn't be happen when the player using the client software. Please be noted.");
                    break;
                default:
                    Log.WriteError($"Unknown code: {result}.");
                    Log.WriteError(new System.Diagnostics.StackTrace().ToString());
                    break;
            }
        }

        private void PlayerOperationRequestHandler(NetworkConnectionToClient conn, PlayerOperationRequest request)
        {
            var response = new PlayerOperationResponse() { Type = request.Type };
            var room = _roomManager.FindRoom(request.RoomId);
            if (room == null)
            {
                response.SuccessCode = -1;
                conn.Send(response);
                return;
            }
            var result = request.Type switch
            {
                PlayerOperationType.Check => _gameManager.Check(request.RoomId, request.PlayerIndex),
                PlayerOperationType.Bet => _gameManager.Bet(request.RoomId, request.PlayerIndex, request.Number),
                PlayerOperationType.Raise => _gameManager.Bet(request.RoomId, request.PlayerIndex, request.Number),
                PlayerOperationType.Call => _gameManager.Call(request.RoomId, request.PlayerIndex),
                PlayerOperationType.AllIn => _gameManager.AllIn(request.RoomId, request.PlayerIndex),
                PlayerOperationType.Fold => _gameManager.Fold(request.RoomId, request.PlayerIndex),
                _ => -1024
            };
            response.SuccessCode = result;
            conn.Send(response);

            // 如果成功，那么就向其他玩家转发新的游戏信息
            if (result != 0)
                return;
            var gameInfo = _gameManager.GetGameInfo(request.RoomId);
            for(int i = 0; i < gameInfo.PlayersCount; i++)
            {
                var name = gameInfo.RoomInfo.Players[i];
                var playerConn = _playerManager.GetConnection(name);

                var sync = new GameInfoSync() { Info = new ClientGameInfo(gameInfo, i) };
                playerConn?.Send(sync);
            }
        }
        private void PlayerOperationResponseHandler(PlayerOperationResponse response)
        {
            switch (response.Type)
            {
                case PlayerOperationType.Check:
                    switch (response.SuccessCode)
                    {
                        case 0:
                            Log.Write("Successful Request");
                            break;
                        case -1:
                            Log.WriteError("Room doesn't exists.");
                            break;
                        case -2:
                            Log.WriteError("Cannot find current Player.");
                            break;
                        case -3:
                            Log.WriteError("Another player is operating.");
                            break;
                        case -4:
                            Log.WriteError("Cannot check because former player bet or raised.");
                            break;
                    }
                    break;
                case PlayerOperationType.Bet:
                    switch (response.SuccessCode)
                    {
                        case 0:
                            Log.Write("Successful Request");
                            break;
                        case -1:
                            Log.WriteError("Room doesn't exists.");
                            break;
                        case -2:
                            Log.WriteError("Cannot find current Player.");
                            break;
                        case -3:
                            Log.WriteError("Another player is operating.");
                            break;
                        case -4:
                            Log.WriteError("The current player has too few chips to bet or raise.");
                            break;
                        case -5:
                            Log.WriteError("Bet or Raise more chips to meet the least bet threshold.");
                            break;
                    }
                    break;
                case PlayerOperationType.Call:
                    switch (response.SuccessCode)
                    {
                        case 0:
                            Log.Write("Successful Request");
                            break;
                        case -1:
                            Log.WriteError("Room doesn't exists.");
                            break;
                        case -2:
                            Log.WriteError("Cannot find current Player.");
                            break;
                        case -3:
                            Log.WriteError("Another player is operating.");
                            break;
                        case -4:
                            Log.WriteError("The current player has too few chips to call.");
                            break;
                    }
                    break;
                case PlayerOperationType.AllIn:
                    switch (response.SuccessCode)
                    {
                        case 0:
                            Log.Write("Successful Request");
                            break;
                        case -1:
                            Log.WriteError("Room doesn't exists.");
                            break;
                        case -2:
                            Log.WriteError("Cannot find current Player.");
                            break;
                        case -3:
                            Log.WriteError("Another player is operating.");
                            break;
                    }
                    break;
                case PlayerOperationType.Fold:
                    switch (response.SuccessCode)
                    {
                        case 0:
                            Log.Write("Successful Request");
                            break;
                        case -1:
                            Log.WriteError("Room doesn't exists.");
                            break;
                        case -2:
                            Log.WriteError("Cannot find current Player.");
                            break;
                        case -3:
                            Log.WriteError("Another player is operating.");
                            break;
                        case -4:
                            Log.WriteError("Big blind cannot fold in preflop stage when all other player choose to call.");
                            break;
                    }
                    break;
            }
        }

        private void PlayerStartGameRequestHandler(NetworkConnectionToClient conn, PlayerStartGameRequest message)
        {
            var response = new PlayerStartGameResponse();
            var room = _roomManager.FindRoom(message.RoomId);
            if (room == null)
            {
                response.SuccessCode = -1;
            }
            else
            {
                response.SuccessCode = _roomManager.StartGame(message.RoomId);
            }

            if (response.SuccessCode != 0)
            {
                conn.Send(response);
                return;
            }

            _gameManager.CreateGame(room);
            response.SuccessCode = _gameManager.StartNew(room.Id);
            if (response.SuccessCode != 0)
            {
                conn.Send(response);
                return;
            }

            var gameInfo = _gameManager.GetGameInfo(room.Id);

            // 如果成功开启游戏，还向其他所有玩家广播游戏开始的信息
            for(int i = 0; i < room.PlayerCount; i++)
            {
                var name = room.Players[i];
                var playerConn = _playerManager.GetConnection(name);
                playerConn?.Send(response);
                playerConn?.Send(new GameInfoSync() { Info = new ClientGameInfo(gameInfo, i) });
            }

        }

        private void PlayerStartGameResponseHandler(PlayerStartGameResponse message)
        {
            switch (message.SuccessCode)
            {
                case 0:
                    UIManager.Instance.HidePanel<RoomPanel>();
                    UIManager.Instance.ShowPanel<GamePanel>();
                    break;
                case -1:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "开始游戏时出错";
                        msgbox.Message = "房间不存在";
                    });
                    UIManager.Instance.HidePanel<RoomPanel>();
                    UIManager.Instance.ShowPanel<RoomListPanel>();
                    break;
                case -2:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "提示";
                        msgbox.Message = "其他玩家尚未全部准备";
                    });
                    break;
                case -3:
                    // this is a rare situation, add this to prevent fault
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "开始游戏时出错";
                        msgbox.Message = "房主不见了";
                    });
                    UIManager.Instance.HidePanel<RoomPanel>();
                    UIManager.Instance.ShowPanel<RoomListPanel>();
                    break;
                case -4:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "开始游戏时出错";
                        msgbox.Message = "你不是房主";
                    });
                    break;
                case -5:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "开始游戏时出错";
                        msgbox.Message = "需要大于等于两个玩家才能开始游戏";
                    });
                    break;
            }
        }

        private void RoomInfoChangeHandler(NetworkConnectionToClient conn, RoomInfoChange message)
        {
            var room = _roomManager.FindRoom(message.RoomId);
            if (room == null)
            {
                Log.WriteWarn($"Player from {conn.address} called {message.PlayerName} requested to change the room info of room {message.RoomId} but the room doesn't exists.");
                return;
            }
            if (room.OwnerName != message.PlayerName)
            {
                Log.WriteWarn($"Non-owner {message.PlayerName} requested to change the room {message.RoomId}");
                return;
            }

            room.InitialChips = message.InitialChips;
            room.ThinkingTime = message.ThinkingTime;
            room.MaxPlayerCount = message.MaxPlayer;

            RoomInfoSync sync = new RoomInfoSync();
            sync.RoomInfo = room;
            
            foreach(var player in room.Players)
            {
                var playerConn = _playerManager.GetConnection(player);
                playerConn?.Send(sync);
            }
        }

        private void RoomInfoSyncHandler(RoomInfoSync message)
        {
            RoomPanel.Instance.RoomInfo = message.RoomInfo;
        }

        private void RoomListRequestHandler(NetworkConnectionToClient conn, RoomListRequest request)
        {
            RoomListResponse response = new RoomListResponse();
            response.RoomList = _roomManager.RoomList;
            conn.Send(response);
        }

        private void RoomListResponseHandler(RoomListResponse response)
        {
            RoomListPanel.Instance.Rooms = response.RoomList;
        }


        private void RoundResultNotificationHandler(RoundResultNotification message)
        {
            UIManager.Instance.ShowPanel<RoundResultMsgBox>(UIManager.Layer.System);
            RoundResultMsgBox.Instance.Info = message.RoundResult;
        }

        #endregion

        #region EventListener

        private void OnGameInfoChanged(int roomId)
        {
            var gameInfo = _gameManager.GetGameInfo(roomId);
            if (gameInfo == null)
                return;
            for (int i = 0; i < gameInfo.PlayersCount; i++)
            {
                var name = gameInfo.RoomInfo.Players[i];
                if (gameInfo.Players[i].IsDisconnected)
                    continue;
                var conn = _playerManager.GetConnection(name);
                var sync = new GameInfoSync()
                {
                    Info = new ClientGameInfo(gameInfo, i)
                };
                conn?.Send(sync);
            }
#if UNITY_EDITOR
            Debug.Log(JsonSerializer.Serialize(gameInfo, new JsonSerializerOptions() { WriteIndented = true}));
#endif
        }

        private async void OnGameStageEnd(int roomId)
        {
            var gameInfo = _gameManager.GetGameInfo(roomId);
            if (gameInfo == null)
                return;
            gameInfo.OperatingPlayer = -1;
            OnGameInfoChanged(roomId);
            // 等待一段时间
            await UniTask.WaitForSeconds(2);
            _gameManager.NextStage(roomId);
        }

        private async void OnRoomRoundEnd(int roomId, RoundResult result)
        {
            var gameInfo = _gameManager.GetGameInfo(roomId);
            if (gameInfo == null)
                return;
            for (int i = 0; i < gameInfo.PlayersCount; i++)
            {
                var name = gameInfo.RoomInfo.Players[i];
                if (gameInfo.Players[i].IsDisconnected)
                    continue;
                var conn = _playerManager.GetConnection(name);
                var roundResult = new RoundResultNotification()
                {
                    RoundResult = result
                };
                conn?.Send(roundResult);
            }

            gameInfo.Timer.Stop();
            await UniTask.WaitForSeconds(5);

            if(gameInfo.Stage == GameStage.RoundOver)
                _gameManager.StartNew(roomId);
        }
        private void OnGameOver(int roomId, GameResult result)
        {
            var gameInfo = _gameManager.GetGameInfo(roomId);
            if (gameInfo == null)
                return;

            gameInfo.Timer.Stop();
            for (int i = 0; i < gameInfo.PlayersCount; i++)
            {
                var name = gameInfo.RoomInfo.Players[i];
                if (gameInfo.Players[i].IsDisconnected)
                    continue;
                var conn = _playerManager.GetConnection(name);
                var gameResult = new GameResultNotification()
                {
                    GameResult = result
                };
                conn?.Send(gameResult);
            }

            _roomManager.DeleteRoom(roomId);
            _gameManager.GameOver(roomId);
        }
        #endregion
    }
}


