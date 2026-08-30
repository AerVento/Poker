using Assets.Scripts.Network.Messages;
using Cysharp.Threading.Tasks;
using Framework.Log;
using Framework.UI;
using Game.Network.Accounts;
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

        private bool _declineRoomRecoveryOnNextLogin;

        #endregion

        public override void Awake()
        {
            if (authenticator == null)
                authenticator = GetComponent<AccountAuthenticator>() ?? gameObject.AddComponent<AccountAuthenticator>();

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
            if (authenticator is AccountAuthenticator accountAuthenticator)
                _gameManager.SetAccountRepository(accountAuthenticator.AccountRepository);
            NetworkServer.RegisterHandler<PlayerChangeReadyMessage>(PlayerChangeReadyMessageHandler, requireAuthentication: true);
            NetworkServer.RegisterHandler<PlayerCreateRoomRequest>(PlayerCreateRoomRequestHandler, requireAuthentication: true);
            NetworkServer.RegisterHandler<PlayerJoinRoomRequest>(PlayerJoinRoomRequestHandler, requireAuthentication: true);
            NetworkServer.RegisterHandler<PlayerLeaveRoomRequest>(PlayerLeaveRoomRequestHandler, requireAuthentication: true);
            NetworkServer.RegisterHandler<PlayerOperationRequest>(PlayerOperationRequestHandler, requireAuthentication: true);
            NetworkServer.RegisterHandler<PlayerStartGameRequest>(PlayerStartGameRequestHandler, requireAuthentication: true);
            NetworkServer.RegisterHandler<RoomInfoChange>(RoomInfoChangeHandler, requireAuthentication: true);
            NetworkServer.RegisterHandler<RoomListRequest>(RoomListRequestHandler, requireAuthentication: true);
            NetworkServer.RegisterHandler<PlayerRoomRecoveryRequest>(PlayerRoomRecoveryRequestHandler, requireAuthentication: true);
        }

        internal NetworkConnectionToClient BindAuthenticatedAccount(
            PlayerAccount account,
            NetworkConnectionToClient conn)
        {
            return _playerManager.BindAccount(account, conn);
        }

        internal void ValidateLastRoom(PlayerAccount account)
        {
            if (account == null || !account.LastRoomId.HasValue)
                return;

            if (_roomManager.FindRoom(account.LastRoomId.Value) != null)
                return;

            if (authenticator is AccountAuthenticator accountAuthenticator &&
                accountAuthenticator.AccountRepository?.SetLastRoom(account.AccountId, null) == true)
            {
                account.LastRoomId = null;
            }
        }

        internal void RestoreReplacedSession(string playerName, NetworkConnectionToClient conn)
        {
            if (!_roomManager.WhichRoom(playerName, out var roomId))
                return;

            SendCurrentRoomState(conn, roomId, playerName, reconnectCurrentHand: true);
        }

        internal bool CanRestoreReplacedSession(PlayerAccount account)
        {
            return account != null &&
                account.LastRoomId.HasValue &&
                _roomManager.WhichRoom(account.UserName, out var roomId) &&
                roomId == account.LastRoomId.Value;
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (_playerManager.RemoveConnection(conn, out var name))
            {

                Log.WriteInfo($"Player {name} from {conn.address} is disconnected.");
                if (_roomManager.WhichRoom(name, out var roomId))
                {
                    var room = _roomManager.FindRoom(roomId);
                    // 等待大厅中断线时立即释放参赛座位，但保留旁观成员和 LastRoomId。
                    if (room.IsWaiting)
                    {
                        _roomManager.MoveToSpectator(roomId, name);
                        BroadcastRoomInfo(room);
                    }
                    else // 游戏已经开始
                    {
                        _gameManager.SetDisconnected(roomId, name);   
                    }
                }
            }

            base.OnServerDisconnect(conn);
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
        }

        internal void HandleClientAuthentication(AccountAuthenticationResponse response)
        {
            if (response.Code != AccountAuthenticationCode.Success || !response.HasLastRoom)
            {
                _declineRoomRecoveryOnNextLogin = false;
                return;
            }

            if (!response.RequiresRoomRecovery)
            {
                _declineRoomRecoveryOnNextLogin = false;
                return;
            }

            if (_declineRoomRecoveryOnNextLogin)
            {
                _declineRoomRecoveryOnNextLogin = false;
                SendRoomRecoveryDecision(response.LastRoomId, false);
                return;
            }

            MsgBox.Create("RoomRecovery", msgbox =>
            {
                msgbox.Type = MsgboxType.ConfirmAndCancel;
                msgbox.ShowCloseButton = false;
                msgbox.Title = "返回上次房间";
                msgbox.Message = $"上次所在的房间 {response.LastRoomId} 仍然存在，是否返回？";
                msgbox.ConfirmButtonText = "返回房间";
                msgbox.CancelButtonText = "离开房间";
                msgbox.OnConfirm += () => SendRoomRecoveryDecision(response.LastRoomId, true);
                msgbox.OnCancel += () => SendRoomRecoveryDecision(response.LastRoomId, false);
            });
        }

        private static void SendRoomRecoveryDecision(int roomId, bool returnToRoom)
        {
            if (GameController.Available && !returnToRoom)
                GameController.Instance.State.SetLastRoom(null);

            NetworkClient.Send(new PlayerRoomRecoveryRequest
            {
                RoomId = roomId,
                ReturnToRoom = returnToRoom,
            });
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

                        var hadRoomAssociation = GameController.Available &&
                            GameController.Instance.State.LastRoomId.HasValue;
                        if (oldConnectedStatus && hadRoomAssociation)
                        {
                            msgbox.Title = "与服务器断开连接";
                            msgbox.Message = "可以重新登录并恢复状态；选择离开房间后，将在下次登录时清除上次房间关联。";
                            msgbox.Type = MsgboxType.ConfirmAndCancel;
                            msgbox.ShowCloseButton = false;
                            msgbox.ConfirmButtonText = "重新登录";
                            msgbox.CancelButtonText = "离开房间";
                            msgbox.OnConfirm += () => _declineRoomRecoveryOnNextLogin = false;
                            msgbox.OnCancel += () => _declineRoomRecoveryOnNextLogin = true;
                        }
                        else if (oldConnectedStatus)
                        {
                            msgbox.Title = "与服务器断开连接";
                            msgbox.Message = "请检查网络连接；如果此账号在其他设备登录，原连接也会被替换。";
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

        private bool TryGetAuthenticatedPlayer(
            NetworkConnectionToClient conn,
            out string playerName)
        {
            return _playerManager.TryGetPlayerName(conn, out playerName);
        }

        private bool PersistLastRoom(string playerName, int? roomId)
        {
            if (!_playerManager.TryGetAccount(playerName, out var account) ||
                !(authenticator is AccountAuthenticator accountAuthenticator) ||
                accountAuthenticator.AccountRepository == null)
            {
                return false;
            }

            try
            {
                if (!accountAuthenticator.AccountRepository.SetLastRoom(account.AccountId, roomId))
                    return false;
                _playerManager.SetLastRoom(playerName, roomId);
                return true;
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to persist LastRoomId for {playerName}: {exception}");
                return false;
            }
        }

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

        private void PlayerChangeReadyMessageHandler(NetworkConnectionToClient conn, PlayerChangeReadyMessage message)
        {
            if (!TryGetAuthenticatedPlayer(conn, out var playerName))
                return;

            int result = _roomManager.PlayerChangeReady(message.RoomId, playerName);
            if(result == 0)
            {
                var room = _roomManager.FindRoom(message.RoomId);
                var sync = new RoomInfoSync() { RoomInfo = room };
                foreach(var player in room.MemberNames)
                {
                    var playerConn = _playerManager.GetConnection(player);
                    playerConn?.Send(sync);
                }
            }
        }
        
        private void PlayerCreateRoomRequestHandler(NetworkConnectionToClient conn, PlayerCreateRoomRequest message)
        {
            if (!TryGetAuthenticatedPlayer(conn, out var playerName))
                return;

            var room = _roomManager.CreateRoom(playerName);
            if (room == null)
            {
                Log.WriteWarn($"Player {playerName} attempted to create a room while already in one.");
                return;
            }
            if (!PersistLastRoom(playerName, room.Id))
            {
                _roomManager.Leave(room.Id, playerName);
                Log.WriteError($"Unable to associate player {playerName} with newly created room {room.Id}.");
                return;
            }
            var sync = new RoomInfoSync() { RoomInfo = room };
            conn.Send(sync);
        }

        private void PlayerJoinRoomRequestHandler(NetworkConnectionToClient conn, PlayerJoinRoomRequest request)
        {
            if (!TryGetAuthenticatedPlayer(conn, out var playerName))
                return;

            var targetRoom = _roomManager.FindRoom(request.RoomId);
            var accountRepository = (authenticator as AccountAuthenticator)?.AccountRepository;
            PlayerAccount account = null;
            try
            {
                if (_playerManager.TryGetAccount(conn, out var sessionAccount))
                    account = accountRepository?.FindById(sessionAccount.AccountId);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to load account for {playerName} while joining room: {exception}");
            }
            int result;
            if (targetRoom == null)
                result = -1;
            else if (account == null)
                result = -5;
            else if (account.Chips < checked((uint)targetRoom.SmallBlindChips * 2U))
                result = -6;
            else
                result = _roomManager.Join(request.RoomId, playerName);
            var response = new PlayerJoinRoomResponse()
            {
                SuccessCode = result,
            };
            if (result == 0)
            {
                var room = _roomManager.FindRoom(request.RoomId);
                if (!PersistLastRoom(playerName, request.RoomId))
                {
                    _roomManager.Leave(request.RoomId, playerName);
                    response.SuccessCode = -5;
                    conn.Send(response);
                    return;
                }
                if (!room.IsWaiting && _gameManager.SynchronizeRoomSeats(request.RoomId) != 0)
                {
                    _roomManager.Leave(request.RoomId, playerName);
                    PersistLastRoom(playerName, null);
                    response.SuccessCode = -5;
                    conn.Send(response);
                    return;
                }

                response.RoomInfo = room;
                conn.Send(response);
                if (!room.IsWaiting)
                {
                    var playerIndex = _gameManager.FindPlayerSeat(request.RoomId, playerName);
                    var gameInfo = _gameManager.GetGameInfo(request.RoomId);
                    conn.Send(new PlayerStartGameResponse { SuccessCode = 0 });
                    conn.Send(new GameInfoSync { Info = new ClientGameInfo(gameInfo, playerIndex) });
                }

                BroadcastRoomInfo(room, playerName);
                BroadcastGameInfo(request.RoomId);
            }
            else
                conn.Send(response);
        }

        private void PlayerRoomRecoveryRequestHandler(
            NetworkConnectionToClient conn,
            PlayerRoomRecoveryRequest request)
        {
            if (!_playerManager.TryGetAccount(conn, out var sessionAccount))
                return;

            PlayerAccount account;
            try
            {
                account = (authenticator as AccountAuthenticator)?.AccountRepository?
                    .FindById(sessionAccount.AccountId);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to load account {sessionAccount.AccountId} during room recovery: {exception}");
                return;
            }

            if (account == null || account.LastRoomId != request.RoomId)
                return;

            var playerName = account.UserName;
            if (!request.ReturnToRoom)
            {
                if (_roomManager.WhichRoom(playerName, out var associatedRoomId) &&
                    associatedRoomId == request.RoomId)
                {
                    var deferRemoval = _gameManager.IsHandInProgress(request.RoomId);
                    if (_roomManager.Leave(request.RoomId, playerName, deferRemoval) == 0)
                    {
                        if (deferRemoval)
                            _gameManager.SetLeaving(request.RoomId, playerName);
                        else
                            _gameManager.SynchronizeRoomSeats(request.RoomId);

                        var remainingRoom = _roomManager.FindRoom(request.RoomId);
                        if (remainingRoom == null)
                            _gameManager.CancelGame(request.RoomId, "RoomEmpty");
                        else
                        {
                            BroadcastRoomInfo(remainingRoom);
                            BroadcastGameInfo(request.RoomId);
                        }
                    }
                }

                PersistLastRoom(playerName, null);
                conn.Send(new RoomListResponse { RoomList = _roomManager.RoomList });
                return;
            }

            var room = _roomManager.FindRoom(request.RoomId);
            if (room == null)
            {
                PersistLastRoom(playerName, null);
                conn.Send(new PlayerJoinRoomResponse { SuccessCode = -1 });
                return;
            }

            var bigBlind = checked((uint)room.SmallBlindChips * 2U);
            var restoreResult = _roomManager.Restore(request.RoomId, playerName, account.Chips >= bigBlind);
            if (restoreResult < 0)
            {
                conn.Send(new PlayerJoinRoomResponse { SuccessCode = restoreResult });
                return;
            }

            SendCurrentRoomState(conn, request.RoomId, playerName, reconnectCurrentHand: true);
            BroadcastRoomInfo(room, playerName);
            BroadcastGameInfo(request.RoomId);
        }

        private void SendCurrentRoomState(
            NetworkConnectionToClient conn,
            int roomId,
            string playerName,
            bool reconnectCurrentHand)
        {
            var room = _roomManager.FindRoom(roomId);
            if (room == null)
                return;

            conn.Send(new PlayerJoinRoomResponse
            {
                SuccessCode = 0,
                RoomInfo = room,
            });
            if (room.IsWaiting)
                return;

            if (_gameManager.SynchronizeRoomSeats(roomId) != 0)
                return;

            var playerIndex = _gameManager.FindPlayerSeat(roomId, playerName);
            if (reconnectCurrentHand && playerIndex >= 0)
                playerIndex = _gameManager.SetConnected(roomId, playerName);

            var gameInfo = _gameManager.GetGameInfo(roomId);
            if (gameInfo == null)
                return;

            conn.Send(new PlayerStartGameResponse { SuccessCode = 0 });
            conn.Send(new GameInfoSync
            {
                Info = new ClientGameInfo(gameInfo, playerIndex),
            });
        }

        private void PlayerJoinRoomResponseHandler(PlayerJoinRoomResponse response)
        {
            int result = response.SuccessCode;
            switch (result)
            {
                case 0:
                    if (GameController.Available)
                        GameController.Instance.State.SetLastRoom(response.RoomInfo.Id);
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
                case -5:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "加入房间时出错";
                        msgbox.Message = "账户或牌局状态暂时不可用";
                    });
                    return;
                case -6:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "筹码不足";
                        msgbox.Message = "至少拥有一个大盲的筹码才能加入牌局";
                    });
                    return;
                default:
                    Log.WriteError($"Unknown success code: {result}.");
                    Log.WriteError(new System.Diagnostics.StackTrace().ToString());
                    return;
            }
        }

        private void PlayerLeaveRoomRequestHandler(NetworkConnectionToClient conn, PlayerLeaveRoomRequest message)
        {
            if (!TryGetAuthenticatedPlayer(conn, out var playerName))
                return;

            var deferRemoval = _gameManager.IsHandInProgress(message.RoomId);
            int result = _roomManager.Leave(message.RoomId, playerName, deferRemoval);
            switch (result)
            {
                case 0:
                    PersistLastRoom(playerName, null);
                    var room = _roomManager.FindRoom(message.RoomId);
                    if (room == null)
                    {
                        _gameManager.CancelGame(message.RoomId, "RoomEmpty");
                        break;
                    }

                    if (deferRemoval)
                        _gameManager.SetLeaving(message.RoomId, playerName);
                    else
                        _gameManager.SynchronizeRoomSeats(message.RoomId);
                    BroadcastRoomInfo(room);
                    BroadcastGameInfo(message.RoomId);
                    break;
                case -1:
                    Log.WriteError($"Player {playerName} requested to leave the room {message.RoomId}, which does not exist.");
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
            var gameInfo = _gameManager.GetGameInfo(request.RoomId);
            if (!_playerManager.TryGetPlayerName(conn, out var playerName) ||
                gameInfo == null ||
                request.PlayerIndex < 0 ||
                request.PlayerIndex >= gameInfo.PlayersCount ||
                !gameInfo.Players[request.PlayerIndex].IsOccupied ||
                gameInfo.Players[request.PlayerIndex].PlayerName != playerName)
            {
                response.SuccessCode = -2;
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
            BroadcastGameInfo(request.RoomId);
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
                        case -6:
                            Log.WriteError("A short all-in did not reopen raising for this player.");
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
                        case -4:
                            Log.WriteError("The current player cannot move all-in.");
                            break;
                        case -5:
                            Log.WriteError("A short all-in did not reopen raising for this player.");
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
                            Log.WriteError("The current player cannot fold.");
                            break;
                    }
                    break;
            }
        }

        private void PlayerStartGameRequestHandler(NetworkConnectionToClient conn, PlayerStartGameRequest message)
        {
            var response = new PlayerStartGameResponse();
            var room = _roomManager.FindRoom(message.RoomId);
            var authenticated = TryGetAuthenticatedPlayer(conn, out var playerName);
            if (room == null)
            {
                response.SuccessCode = -1;
            }
            else if (!authenticated || room.OwnerName != playerName)
            {
                response.SuccessCode = -4;
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

            response.SuccessCode = _gameManager.CreateGame(room);
            if (response.SuccessCode == 0)
                response.SuccessCode = _gameManager.StartNew(room.Id);
            if (response.SuccessCode != 0)
            {
                _gameManager.CancelGame(room.Id, "StartFailed");
                _roomManager.ReturnToWaiting(room.Id);
                conn.Send(response);
                return;
            }

            // 如果成功开启游戏，还向其他所有玩家广播游戏开始的信息
            foreach (var name in room.MemberNames)
                _playerManager.GetConnection(name)?.Send(response);
            BroadcastGameInfo(room.Id);

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
                case -6:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "开始游戏时出错";
                        msgbox.Message = "该房间的游戏已经开始";
                    });
                    break;
            }
        }

        private void RoomInfoChangeHandler(NetworkConnectionToClient conn, RoomInfoChange message)
        {
            if (!TryGetAuthenticatedPlayer(conn, out var playerName))
                return;

            var room = _roomManager.FindRoom(message.RoomId);
            if (room == null)
            {
                Log.WriteWarn($"Player {playerName} requested to change missing room {message.RoomId}.");
                return;
            }
            if (!room.IsWaiting)
            {
                Log.WriteWarn($"Player {playerName} attempted to change settings after room {message.RoomId} started.");
                return;
            }
            if (room.OwnerName != playerName)
            {
                Log.WriteWarn($"Non-owner {playerName} requested to change room {message.RoomId}.");
                return;
            }
            if (message.SmallBlindChips <= 0 ||
                message.ThinkingTime <= 0 ||
                message.MaxPlayer < 2 ||
                message.MaxPlayer > 16 ||
                message.MaxPlayer < room.PlayerCount)
            {
                Log.WriteWarn($"Player {playerName} sent invalid room settings for room {message.RoomId}.");
                return;
            }

            if (!_roomManager.ResizeSeats(message.RoomId, message.MaxPlayer))
                return;

            room.SmallBlindChips = message.SmallBlindChips;
            room.ThinkingTime = message.ThinkingTime;

            RoomInfoSync sync = new RoomInfoSync();
            sync.RoomInfo = room;
            
            foreach(var player in room.MemberNames)
            {
                var playerConn = _playerManager.GetConnection(player);
                playerConn?.Send(sync);
            }
        }

        private void RoomInfoSyncHandler(RoomInfoSync message)
        {
            if (GameController.Available &&
                message.RoomInfo.ContainsMember(GameController.Instance.State.UserName))
            {
                GameController.Instance.State.SetLastRoom(message.RoomInfo.Id);
            }
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
            BroadcastGameInfo(roomId);
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
            var room = _roomManager.FindRoom(roomId);
            if (room == null)
                return;
            foreach (var name in room.MemberNames)
            {
                var conn = _playerManager.GetConnection(name);
                var roundResult = new RoundResultNotification()
                {
                    RoundResult = result
                };
                conn?.Send(roundResult);
            }

            gameInfo.Timer.Stop();
            _roomManager.ReleaseWatchingSeats(roomId);
            if (!_roomManager.FinalizeDepartures(roomId))
            {
                _gameManager.CancelGame(roomId, "RoomEmpty");
                return;
            }

            room = _roomManager.FindRoom(roomId);
            if (_gameManager.SynchronizeRoomSeats(roomId) != 0)
            {
                ReturnRoomToLobby(roomId);
                return;
            }
            BroadcastRoomInfo(room);
            if (_gameManager.CountEligiblePlayers(roomId) < 2)
            {
                ReturnRoomToLobby(roomId);
                return;
            }

            await UniTask.WaitForSeconds(10);

            room = _roomManager.FindRoom(roomId);
            gameInfo = _gameManager.GetGameInfo(roomId);
            if (room == null || gameInfo == null || room.IsWaiting || gameInfo.Stage != GameStage.RoundOver)
                return;

            if (_gameManager.SynchronizeRoomSeats(roomId) != 0 ||
                _gameManager.CountEligiblePlayers(roomId) < 2 ||
                _gameManager.StartNew(roomId) != 0)
            {
                ReturnRoomToLobby(roomId);
                return;
            }

            BroadcastGameInfo(roomId);
        }
        private void OnGameOver(int roomId, GameResult result)
        {
            var gameInfo = _gameManager.GetGameInfo(roomId);
            if (gameInfo == null)
                return;

            gameInfo.Timer.Stop();
            ReturnRoomToLobby(roomId);
        }

        private void BroadcastRoomInfo(RoomInfo room, string exceptPlayer = null)
        {
            if (room == null)
                return;

            var sync = new RoomInfoSync { RoomInfo = room };
            foreach (var playerName in room.MemberNames)
            {
                if (playerName == exceptPlayer)
                    continue;
                _playerManager.GetConnection(playerName)?.Send(sync);
            }
        }

        private void BroadcastGameInfo(int roomId)
        {
            var gameInfo = _gameManager.GetGameInfo(roomId);
            if (gameInfo == null)
                return;

            for (int seatIndex = 0; seatIndex < gameInfo.PlayersCount; seatIndex++)
            {
                var player = gameInfo.Players[seatIndex];
                if (!player.IsOccupied || player.IsDisconnected || player.IsLeaving)
                    continue;

                var conn = _playerManager.GetConnection(player.PlayerName);
                conn?.Send(new GameInfoSync
                {
                    Info = new ClientGameInfo(gameInfo, seatIndex),
                });
            }

            var room = _roomManager.FindRoom(roomId);
            if (room == null)
                return;
            foreach (var spectatorName in room.Spectators)
            {
                _playerManager.GetConnection(spectatorName)?.Send(new GameInfoSync
                {
                    Info = new ClientGameInfo(gameInfo, -1),
                });
            }
        }

        private void ReturnRoomToLobby(int roomId)
        {
            var room = _roomManager.FindRoom(roomId);
            if (room == null)
            {
                _gameManager.CancelGame(roomId, "RoomMissing");
                return;
            }

            _roomManager.ReturnToWaiting(roomId);
            _gameManager.CancelGame(roomId, "ReturnedToLobby");
            BroadcastRoomInfo(room);
        }
        #endregion
    }
}


