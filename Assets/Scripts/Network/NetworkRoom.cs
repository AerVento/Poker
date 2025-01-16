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
        #region Server Only
        // server only
        private RoomManager _roomManager = new RoomManager();
        private PlayerManager _playerManager = new PlayerManager();

        #endregion

        #region Client Only
        // Client Only
        /// <summary>
        /// ��ʶ��ǰ�Ƿ����ӵ����������ͻ���ʹ�ã�
        /// ��ʹ��<see cref="NetworkClient.isConnected"/>��ԭ������Ϊ����<see cref="NetworkManager.OnClientDisconnect"/>ʱ���Ѿ�����ֵ��Ϊfalse���⽫ʹ���޴�֪���Ƿ������������ӹ���
        /// </summary>
        private bool _isConnected = false;

        /// <summary>
        /// "�������ӷ�����"��MsgBox
        /// </summary>
        private MsgBox _connectingMsgbox;

        #endregion

        public override void Awake()
        {
            base.Awake();
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
            NetworkServer.RegisterHandler<PlayerStartGameRequest>(PlayerStartGameRequestHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<RoomInfoChange>(RoomInfoChangeHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<RoomListRequest>(RoomListRequestHandler, requireAuthentication: false);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // base.OnServerDisconnect(conn);
            if (_playerManager.RemoveConnection(conn, out var name))
            {
                if (_roomManager.WhichRoom(name, out var roomId))
                {
                    int result = _roomManager.Leave(roomId, name);
                    switch (result)
                    {
                        case 0:
                            var room = _roomManager.FindRoom(roomId);
                            var sync = new RoomInfoSync() { RoomInfo = room };
                            foreach (var player in room.Players)
                            {
                                var playerConn = _playerManager.GetConnection(player);
                                playerConn.Send(sync);
                            }
                            break;
                    }
                }
            }
        }
        #endregion Server
        #region Client
        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<PlayerJoinRoomResponse>(PlayerJoinRoomResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<PlayerStartGameResponse>(PlayerStartGameResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<RoomInfoSync>(RoomInfoSyncHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<RoomListResponse>(RoomListResponseHandler, requireAuthentication: false);

            
            MsgBox.Create("ConnectingServer", (setting) =>
            {
                _connectingMsgbox = setting;
                setting.Type = MsgboxType.Cancel;
                setting.Title = "֪ͨ";
                setting.Message = "�������ӵ�������";
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

            // ��������ʱ�򿪵�Msgbox"ConnectingServer"
            if (_connectingMsgbox != null)
            {
                _connectingMsgbox.Hide();
                _connectingMsgbox = null;
            }

            _isConnected = true;

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
                msgbox.Title = "�޷�������������";
                msgbox.Message = $"{error}:{reason}";
            });
        }

        public override void OnClientTransportException(Exception exception)
        {
            // base.OnClientTransportException(exception);
            MsgBox.Create((msgbox) =>
            {
                msgbox.Title = "�޷�������������";
                msgbox.Message = $"{exception}";
            });
        }



        public override void OnClientDisconnect()
        {
            // base.OnClientDisconnect();

            // ��������ʱ�򿪵�Msgbox
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

                // �ȴ�һ֡���ٴ�TitlePanel
                // �����ȴ���ͬһ֡��Destroy���������Destory������ʱ����һ���µ�TitlePanel����ᵼ��Singleton��ͻ
                UIManager.Instance.ShowPanel<TitlePanel>();
                MsgBox.Create((msgbox) =>
                {

                    if (oldConnectedStatus)
                    {
                        msgbox.Title = "��������Ͽ�����";
                        msgbox.Message = "�п�������״̬�������������\nҲ�м�С��������ͬ����ҽ��룬�뿼�Ǹ����ǳ�";
                    }
                    else
                    {
                        msgbox.Title = "�޷�������������";
                        msgbox.Message = "������������";
                    }
                });
            });


        }

        #endregion

        #region Message Handler

        private void PlayerChangeNameMessageHandler(NetworkConnectionToClient conn, PlayerChangeNameMessage message)
        {
            _playerManager.RemoveConnection(message.OldName);
            _playerManager.SetConnection(message.NewName, conn);
            Log.Write($"Player {message.OldName} from {conn.address} changed its player name to {message.NewName}.");
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
                    playerConn.Send(sync);
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
                        continue; // ����Ǹռ��뷿����ˣ��Ͳ�������Sync�ˣ�response���������ṩroom��Ϣ���Լ����¾Ϳ���
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
                        msgbox.Title = "���뷿��ʱ����";
                        msgbox.Message = "ѡ���ķ��䲻����";
                    });
                    RoomListPanel.Instance.RefreshRoomList();
                    return;
                case -2:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "���뷿��ʱ����";
                        msgbox.Message = $"���{GameController.Instance.State.Nickname}�Ѿ��ڷ�����";
                    });
                    return;
                case -3:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "���뷿��ʱ����";
                        msgbox.Message = "��������";
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

        private void PlayerStartGameRequestHandler(NetworkConnectionToClient conn, PlayerStartGameRequest message)
        {
            Log.WriteError(new NotImplementedException().ToString());
        }

        private void PlayerStartGameResponseHandler(PlayerStartGameResponse message)
        {
            throw new NotImplementedException();
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


