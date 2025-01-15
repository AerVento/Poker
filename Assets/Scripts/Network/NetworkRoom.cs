using Assets.Scripts.Network.Messages;
using Cysharp.Threading.Tasks;
using Framework.Log;
using Framework.UI;
using Game.UI;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Network
{
    public class NetworkRoom : NetworkManager
    {
        // server only
        private RoomManager _roomManager = new RoomManager();
        private PlayerManager _playerManager = new PlayerManager();

        public override void Awake()
        {
            base.Awake();
        }

        public override void OnServerReady(NetworkConnectionToClient conn)
        {
            // base.OnServerReady(conn);
        }
        
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // base.OnServerAddPlayer(conn);
        }

        public override void OnClientConnect()
        {
            // base.OnClientConnect();
            UIManager.Instance.ShowPanel<RoomListPanel>();
            var message = new PlayerConnectMessage();
            message.PlayerName = GameController.Instance.State.Nickname;
            NetworkClient.Send(message);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.RegisterHandler<PlayerConnectMessage>(PlayerConnectMessageHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<PlayerChangeNameMessage>(PlayerChangeNameMessageHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<PlayerJoinRoomRequest>(PlayerJoinRoomRequestHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<PlayerCreateRoomRequest>(PlayerCreateRoomRequestHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<PlayerLeaveRoomRequest>(PlayerLeaveRoomRequestHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<RoomListRequest>(RoomListRequestHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<RoomInfoChange>(RoomInfoChangeHandler, requireAuthentication: false);

        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<RoomListResponse>(RoomListResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<PlayerJoinRoomResponse>(PlayerJoinRoomResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<RoomInfoSync>(RoomInfoSyncHandler, requireAuthentication: false);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // base.OnServerDisconnect(conn);
            if(_playerManager.RemoveConnection(conn, out var name))
            {
                if(_roomManager.WhichRoom(name, out var roomId))
                {
                    int result = _roomManager.Leave(roomId, name);
                    switch (result)
                    {
                        case 0:
                            var room = _roomManager.FindRoom(roomId);
                            var sync = new RoomInfoSync() { RoomInfo = room };
                            foreach(var player in room.Players)
                            {
                                var playerConn = _playerManager.GetConnection(player);
                                playerConn.Send(sync);
                            }
                            break;
                    }
                }
            }
        }

        public override void OnClientDisconnect()
        {
            // base.OnClientDisconnect();
            foreach(var identifier in UIManager.Instance.ShowingPanelIdentifiers.ToList())
            {
                UIManager.Instance.DestroyPanel(identifier);
            }
            UniTask.Void(async () =>
            {
                await UniTask.NextFrame();

                // 等待一帧后再打开TitlePanel
                // 若不等待，同一帧中Destroy并不会真的Destory，若此时出现一个新的TitlePanel，则会导致Singleton冲突
                UIManager.Instance.ShowPanel<TitlePanel>();
                MsgBox.Create((msgbox) =>
                {
                    msgbox.Title = "与服务器断开连接";
                    msgbox.Message = "有可能网络状态差，请检查网络连接\n也有极小可能是有同名玩家进入，请考虑更换昵称";
                });
            });

        }

        #region Message Handler

        private void PlayerChangeNameMessageHandler(NetworkConnectionToClient conn, PlayerChangeNameMessage message)
        {
            _playerManager.RemoveConnection(message.OldName);
            _playerManager.SetConnection(message.NewName, conn);
            Log.Write($"Player {message.OldName} from {conn.address} changed its player name to {message.NewName}.");
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
                    playerConn.Send(sync);
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
                            playerConn.Send(sync);
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
                playerConn.Send(sync);
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

        #endregion
    }
}


