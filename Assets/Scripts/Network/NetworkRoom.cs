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
    public partial class NetworkRoom : NetworkManager
    {
        #region Server Only
        // server only
        private RoomManager _roomManager = new RoomManager();
        private PlayerManager _playerManager = new PlayerManager();
        private GameManager _gameManager = new GameManager();
        private RoomDepartureService _departures;
        private TableParticipationService _participation;
        private int _serverLifecycleVersion;
        private bool _serverStorageFailed;
        private bool _storageShutdownRunning;
        public bool IsServerShuttingDown => _serverStorageFailed;

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

        private bool _intentionalLogout;

        private TableParticipationStatus? _lastParticipationStatus;
        private int? _activeClientRoomId;
        private bool _roomEntryPending;
        private int? _requestedRoomId;

        public bool IsRoomEntryPending => _roomEntryPending;

        private static bool HasAuthenticatedClient => NetworkClient.isConnected &&
            NetworkClient.connection != null && NetworkClient.connection.isAuthenticated &&
            GameController.Available && GameController.Instance.State.IsAuthenticated;

        private void ClearClientRoomNavigation()
        {
            _activeClientRoomId = null;
            _roomEntryPending = false;
            _requestedRoomId = null;
        }

        private static void OpenLobby()
        {
            var ui = UIManager.Instance;
            ui.ResetPanels<TitlePanel>();
            ui.PushPanel<RoomListPanel>();
        }

        public bool RequestCreateRoom()
        {
            if (!HasAuthenticatedClient || _roomEntryPending || _activeClientRoomId.HasValue ||
                GameController.Instance.State.LastRoomId.HasValue)
                return false;
            _roomEntryPending = true;
            _requestedRoomId = null;
            NetworkClient.Send(new PlayerCreateRoomRequest { PlayerName = GameController.Instance.State.Nickname });
            return true;
        }

        public bool RequestJoinRoom(int roomId)
        {
            if (!HasAuthenticatedClient || _roomEntryPending || _activeClientRoomId.HasValue ||
                GameController.Instance.State.LastRoomId.HasValue)
                return false;
            _roomEntryPending = true;
            _requestedRoomId = roomId;
            NetworkClient.Send(new PlayerJoinRoomRequest
            {
                PlayerName = GameController.Instance.State.Nickname,
                RoomId = roomId,
            });
            return true;
        }

        private bool _silentConnectionAttempt;
        private bool _silentDisconnect;
        private int _clientConnectionAttemptVersion;
        private float _clientConnectTimeoutSeconds = 10f;
        private ClientNetworkConfiguration _clientConfiguration;
        private ServerShutdownReason? _serverShutdownReason;
        private bool _spectatorIntroShown;
        private bool _clientTransportFailed;

        #endregion

        public override void Awake()
        {
            if (authenticator == null)
                authenticator = GetComponent<AccountAuthenticator>() ?? gameObject.AddComponent<AccountAuthenticator>();

            base.Awake();
            // Networking starts only after configuration validation; the legacy HUD bypasses that flow.
            var hud = GetComponent<NetworkManagerHUD>();
            if (hud != null)
                hud.enabled = false;
            _departures = new RoomDepartureService(_roomManager, _gameManager,
                name => PersistLastRoom(name, null));
            _participation = new TableParticipationService(_roomManager, _gameManager,
                GetAccountChips, name => _playerManager.IsConnected(name));
            _gameManager.OnGameInfoChanged += OnGameInfoChanged;
            _gameManager.OnGameStageEnded += OnGameStageEnd;
            _gameManager.OnRoomRoundEnded += OnRoomRoundEnd;
            _gameManager.OnRoomGameOver += OnGameOver;
        }

        public override void Start()
        {
            // GameController owns startup, including headless builds, after loading deployment configuration.
        }

        public void ConfigureClient(ClientNetworkConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            configuration.Apply(this);
            _clientConfiguration = configuration;
            _clientConnectTimeoutSeconds = configuration.ConnectTimeoutSeconds;
        }

        public bool StartConfiguredClientConnection()
        {
            if (NetworkClient.active || NetworkServer.active)
                return false;
            if (GameController.Available && GameController.Instance.HasNetworkInitializationStarted &&
                !GameController.Instance.IsClientNetworkReady)
                return false;
            _clientConfiguration?.Apply(this);
            StartClient();
            return true;
        }

        #region Server
        private void RegisterServerHandler<T>(Action<NetworkConnectionToClient, T> handler,
            bool requireAuthentication) where T : struct, NetworkMessage
        {
            NetworkServer.RegisterHandler<T>((conn, message) =>
            {
                try
                {
                    if (!_serverStorageFailed)
                        handler(conn, message);
                }
                catch (Exception) when (_serverStorageFailed)
                {
                    // Keep connections alive until the fatal-storage notice has been flushed.
                }
            }, requireAuthentication);
        }

        internal void HandleFatalStorageError(Exception exception)
        {
            if (_serverStorageFailed)
                return;
            _serverStorageFailed = true;
            _serverLifecycleVersion++;
            _gameManager.StopForStorageFailure();
            Log.WriteError($"Fatal account storage error; stopping server: {exception}");
            if (NetworkServer.active)
            {
                foreach (var conn in NetworkServer.connections.Values.ToArray())
                    SendShutdownNotice(conn);
            }
            StopServerAfterStorageFailure().Forget();
        }

        private static void SendShutdownNotice(NetworkConnectionToClient conn)
        {
            try
            {
                conn?.Send(new ServerShutdownNotice { Reason = ServerShutdownReason.StorageFailure });
            }
            catch (Exception exception)
            {
                Log.WriteWarn($"Unable to deliver server shutdown notice: {exception.Message}");
            }
        }

        private async UniTask StopServerAfterStorageFailure()
        {
            if (_storageShutdownRunning)
                return;
            _storageShutdownRunning = true;
            // Let Mirror flush the reliable notice before closing the transport. Best effort only.
            await UniTask.Delay(500, ignoreTimeScale: true);
            if (this == null)
                return;
            bool hadLocalClient = NetworkClient.active;
            try
            {
                if (mode == NetworkManagerMode.Host)
                    StopHost();
                else if (NetworkServer.active)
                    StopServer();
            }
            finally
            {
                _storageShutdownRunning = false;
                if (!Application.isEditor && !hadLocalClient &&
                    (Application.isBatchMode || Application.platform == RuntimePlatform.LinuxPlayer))
                    Application.Quit(1);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_serverStorageFailed)
            {
                StopServerAfterStorageFailure().Forget();
                return;
            }
            _serverLifecycleVersion++;
            if (authenticator is AccountAuthenticator accountAuthenticator)
                _gameManager.SetAccountRepository(accountAuthenticator.AccountRepository);
            RegisterDailyTaskServerHandlers();
            RegisterLeaderboardServerHandler();
            RegisterShopServerHandlers();
            RegisterServerHandler<PlayerChangeReadyMessage>(PlayerChangeReadyMessageHandler, requireAuthentication: true);
            RegisterServerHandler<PlayerCreateRoomRequest>(PlayerCreateRoomRequestHandler, requireAuthentication: true);
            RegisterServerHandler<PlayerJoinRoomRequest>(PlayerJoinRoomRequestHandler, requireAuthentication: true);
            RegisterServerHandler<PlayerLeaveRoomRequest>(PlayerLeaveRoomRequestHandler, requireAuthentication: true);
            RegisterServerHandler<TableParticipationRequest>(TableParticipationRequestHandler, requireAuthentication: true);
            RegisterServerHandler<PlayerOperationRequest>(PlayerOperationRequestHandler, requireAuthentication: true);
            RegisterServerHandler<PlayerStartGameRequest>(PlayerStartGameRequestHandler, requireAuthentication: true);
            RegisterServerHandler<RoomInfoChange>(RoomInfoChangeHandler, requireAuthentication: true);
            RegisterServerHandler<RoomListRequest>(RoomListRequestHandler, requireAuthentication: true);
            RegisterServerHandler<PlayerRoomRecoveryRequest>(PlayerRoomRecoveryRequestHandler, requireAuthentication: true);
        }

        public override void OnStopServer()
        {
            _serverLifecycleVersion++;
            _dailyTaskRequestSequences.Clear();
            _leaderboardRequests.Clear();
            _shopRequests.Clear();
            _dailyTaskService = null;
            base.OnStopServer();
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
            if (_serverStorageFailed)
            {
                SendShutdownNotice(conn);
                return;
            }
            base.OnServerConnect(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            _dailyTaskRequestSequences.Remove(conn);
            _leaderboardRequests.Remove(conn);
            _shopRequests.Remove(conn);

            if (!_serverStorageFailed && _playerManager.RemoveConnection(conn, out var name))
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
                        if (!_gameManager.IsHandInProgress(roomId))
                        {
                            if (_gameManager.GetGameInfo(roomId)?.Stage != GameStage.RoundOver)
                                _roomManager.ReleaseWatchingSeats(roomId);
                            RefreshRoomAfterDeparture(roomId);
                        }
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
            ClearClientRoomNavigation();
            _serverShutdownReason = null;
            _clientTransportFailed = false;
            NetworkClient.RegisterHandler<ServerShutdownNotice>(ServerShutdownNoticeHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<PlayerLeaveRoomResponse>(PlayerLeaveRoomResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<GameInfoSync>(GameInfoSyncHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<GameResultNotification>(GameResultNotificationHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<PlayerJoinRoomResponse>(PlayerJoinRoomResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<TableParticipationResponse>(TableParticipationResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<PlayerOperationResponse>(PlayerOperationResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<PlayerStartGameResponse>(PlayerStartGameResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<RoomInfoSync>(RoomInfoSyncHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<RoomListResponse>(RoomListResponseHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<RoundResultNotification>(RoundResultNotificationHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<InterHandCountdownSync>(InterHandCountdownSyncHandler, requireAuthentication: false);
            NetworkClient.RegisterHandler<AccountBalanceSync>(AccountBalanceSyncHandler, requireAuthentication: false);
            RegisterDailyTaskClientHandlers();
            RegisterLeaderboardClientHandler();
            RegisterShopClientHandlers();
            
            if (!_silentConnectionAttempt)
                ShowConnectingMessage();
            MonitorClientConnection(++_clientConnectionAttemptVersion, _clientConnectTimeoutSeconds).Forget();
        }

        public void StartSilentClientConnection(float timeoutSeconds)
        {
            if (timeoutSeconds <= 0 || NetworkServer.active || NetworkClient.active)
                return;

            _silentConnectionAttempt = true;
            _silentDisconnect = false;
            _clientConnectTimeoutSeconds = timeoutSeconds;
            if (!StartConfiguredClientConnection())
                _silentConnectionAttempt = false;
        }

        internal void PromoteSilentConnectionAttempt()
        {
            if (!_silentConnectionAttempt || NetworkClient.isConnected)
                return;

            _silentConnectionAttempt = false;
            ShowConnectingMessage();
        }

        private void ShowConnectingMessage()
        {
            if (_connectingMsgbox != null)
                return;

            MsgBox.Create("ConnectingServer", setting =>
            {
                _connectingMsgbox = setting;
                setting.Type = MsgboxType.Cancel;
                setting.Title = "通知";
                setting.Message = "正在连接到服务器";
                setting.ShowCloseButton = false;
                setting.OnCancel += StopClient;
            });
        }

        private async UniTask MonitorClientConnection(int attemptVersion, float timeoutSeconds)
        {
            var cancelled = await UniTask.Delay(
                TimeSpan.FromSeconds(timeoutSeconds), ignoreTimeScale: true,
                cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
            if (cancelled || attemptVersion != _clientConnectionAttemptVersion || NetworkClient.isConnected)
                return;

            if (NetworkClient.active)
            {
                _silentDisconnect = _silentConnectionAttempt;
                StopClient();
            }
            _silentConnectionAttempt = false;
        }

        private void ServerShutdownNoticeHandler(ServerShutdownNotice notice)
        {
            _serverShutdownReason = notice.Reason;
            _silentDisconnect = false;
            _intentionalLogout = false;
            _declineRoomRecoveryOnNextLogin = false;
            if (NetworkClient.active)
                StopClient();
        }

        public static void ConfigureServerShutdownMessage(MsgBox msgbox)
        {
            msgbox.Title = "服务器已停止服务";
            msgbox.Message = "服务器数据保存失败，服务已停止。\n请等待服务器恢复后重新登录。";
            msgbox.Type = MsgboxType.Confirm;
            msgbox.ConfirmButtonText = "知道了";
            msgbox.ShowCloseButton = false;
        }

        public override void OnClientConnect()
        {
            // base.OnClientConnect();
            _silentConnectionAttempt = false;
            _clientConnectionAttemptVersion++;

            // 销毁连接时打开的Msgbox"ConnectingServer"
            if (_connectingMsgbox != null)
            {
                _connectingMsgbox.Hide();
                _connectingMsgbox = null;
            }

            _isConnected = true;
        }

        internal void HandleClientAuthentication(AccountAuthenticationResponse response)
        {
            if (response.Code != AccountAuthenticationCode.Success)
                return;

            var destination = LoginPanel.Instance != null && !response.HasLastRoom
                ? LoginPanel.Instance.DestinationAfterLogin : LoginDestination.Lobby;
            ClearClientRoomNavigation();
            if (destination != LoginDestination.Lobby)
            {
                UIManager.Instance.ResetPanels<TitlePanel>();
                TitlePanel.ShowAccountPage(destination);
            }
            else
                OpenLobby();

            if (!response.HasLastRoom)
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

        internal void HandleClientLogout()
        {
            ClearClientRoomNavigation();
            _intentionalLogout = true;
            _declineRoomRecoveryOnNextLogin = false;
            if (NetworkClient.active)
            {
                // An unfinished connection may stop without an OnClientDisconnect callback.
                bool wasConnected = NetworkClient.isConnected;
                StopClient();
                if (!wasConnected)
                    ShowTitleAfterLogout();
            }
            else
                ShowTitleAfterLogout();
        }

        private static void ShowTitleAfterLogout()
        {
            if (!UIManager.Available)
                return;

            UIManager.Instance.ResetPanels<TitlePanel>();
        }

        private void SendRoomRecoveryDecision(int roomId, bool returnToRoom)
        {
            if (!HasAuthenticatedClient || GameController.Instance.State.LastRoomId != roomId || _roomEntryPending)
                return;
            // Declining recovery can still return the player to an unfinished hand to finish leaving.
            _roomEntryPending = true;
            _requestedRoomId = roomId;
            NetworkClient.Send(new PlayerRoomRecoveryRequest
            {
                RoomId = roomId,
                ReturnToRoom = returnToRoom,
            });
        }

        public override void OnClientError(TransportError error, string reason)
        {
            Log.WriteWarn($"Client transport error: {error}: {reason}");
            HandleClientTransportFailure();
        }

        public override void OnClientTransportException(Exception exception)
        {
            Log.WriteWarn($"Client transport exception: {exception}");
            HandleClientTransportFailure();
        }

        private void HandleClientTransportFailure()
        {
            if (_serverShutdownReason.HasValue || _silentConnectionAttempt || _silentDisconnect || _clientTransportFailed)
                return;
            _clientTransportFailed = true;
            if (NetworkClient.active)
                StopClient();
        }

        private void ReopenLogin()
        {
            _declineRoomRecoveryOnNextLogin = false;
            if (!UIManager.Available)
                return;
            UIManager.Instance.PushPanel<LoginPanel>();
            LoginPanel.Instance.BeginSavedLogin(GameController.Available
                ? GameController.Instance.State.SessionToken : null);
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
            var shutdownReason = _serverShutdownReason;
            bool oldConnectedStatus = _isConnected;
            bool intentionalLogout = _intentionalLogout;
            bool silentDisconnect = _silentDisconnect || _silentConnectionAttempt;
            _silentDisconnect = false;
            _silentConnectionAttempt = false;
            _clientConnectionAttemptVersion++;
            _intentionalLogout = false;
            _isConnected = false;
            ClearClientRoomNavigation();
            GameAudioManager.Existing?.EnterLobby();
            _lastParticipationStatus = null;
            if (!shutdownReason.HasValue && silentDisconnect && !oldConnectedStatus)
                return;

            if (UIManager.Available)
            {
                UIManager.Instance.ResetPanels<TitlePanel>();
                if (shutdownReason.HasValue)
                {
                    MsgBox.Create("ServerShutdown", ConfigureServerShutdownMessage);
                    return;
                }
                if (intentionalLogout || silentDisconnect)
                    return;

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
                        msgbox.OnConfirm += ReopenLogin;
                        msgbox.OnCancel += () => _declineRoomRecoveryOnNextLogin = true;
                    }
                    else if (oldConnectedStatus)
                    {
                        msgbox.Title = "与服务器断开连接";
                        msgbox.Message = "连接已中断，请检查网络后重新登录。若此账号在其他设备登录，原连接也会被替换。";
                        msgbox.ConfirmButtonText = "重新登录";
                        msgbox.OnConfirm += ReopenLogin;
                    }
                    else
                    {
                        msgbox.Title = "无法连接至服务器";
                        msgbox.Message = "暂时无法连接服务器，请检查网络或稍后再试。";
                        msgbox.ConfirmButtonText = "重新登录";
                        msgbox.OnConfirm += ReopenLogin;
                    }
                });
            }
        }

        #endregion

        #region Message Handler

        private bool TryGetAuthenticatedPlayer(
            NetworkConnectionToClient conn,
            out string playerName)
        {
            playerName = null;
            return !_serverStorageFailed && _playerManager.TryGetPlayerName(conn, out playerName);
        }

        private bool PersistLastRoom(string playerName, int? roomId)
        {
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository;
            if (repository == null)
                return false;

            try
            {
                if (!_playerManager.TryGetAccount(playerName, out var account))
                    account = repository.FindByUserName(playerName);
                if (account == null || !repository.SetLastRoom(account.AccountId, roomId))
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
            if (!HasAuthenticatedClient || message.Info.RoomInfo == null ||
                _activeClientRoomId != message.Info.RoomInfo.Id ||
                !message.Info.RoomInfo.ContainsMember(GameController.Instance.State.UserName))
                return;
            if (GameController.Available)
                GameController.Instance.State.SetChips(message.AccountChips);
            if (!UIManager.Instance.ContainsPanel<GamePanel>())
                UIManager.Instance.ResetPanels<GamePanel>();
            if (message.Info.Round < GameStage.RoundOver)
                UIManager.Instance.PopPanel<RoundResultMsgBox>();
            GamePanel.Instance.ApplySync(message);

            if (message.Info.Round != GameStage.RoundOver &&
                message.ParticipationStatus == TableParticipationStatus.InsufficientChips &&
                _lastParticipationStatus != TableParticipationStatus.InsufficientChips)
            {
                ShowInsufficientChipsPrompt(message);
            }
            if (message.Info.IsSpectator && !_spectatorIntroShown)
            {
                _spectatorIntroShown = true;
                if (message.Info.Round != GameStage.RoundOver &&
                    message.ParticipationStatus != TableParticipationStatus.InsufficientChips)
                    MsgBox.Create("SpectatorIntro", ConfigureSpectatorIntro);
            }
            else if (!message.Info.IsSpectator)
                UIManager.Instance.DestroyPanel("SpectatorIntro");
            _lastParticipationStatus = message.ParticipationStatus;
            Log.WriteInfo("Game Sync:" + JsonSerializer.Serialize(message.Info, new JsonSerializerOptions() { WriteIndented = true }));
        }

        public static void ConfigureSpectatorIntro(MsgBox box)
        {
            box.Title = "你正在旁观";
            box.Message = "当前不会为你发牌。可点击“下一手参战”申请入座，也可继续旁观或离开。\n名额只统计参战玩家，满桌时需等待空位。";
            box.Type = MsgboxType.Confirm;
            box.ConfirmButtonText = "知道了";
        }

        public static void ConfigureRoomTransition(MsgBox box, RoomTransitionReason reason, bool spectator)
        {
            box.Title = reason == RoomTransitionReason.InsufficientPlayers ? "参战人数不足" : "本桌对局已结束";
            box.Message = reason == RoomTransitionReason.InsufficientPlayers
                ? spectator ? "下一手参战人数不足两人，本桌已停止对局。\n你已返回房间列表。"
                    : "下一手参战人数不足两人，暂时无法继续。\n你已返回房间等待，可等待其他玩家加入。"
                : spectator ? "本桌已停止对局，你已返回房间列表。" : "本桌已停止对局，你已返回房间等待。";
            box.Type = MsgboxType.Confirm;
            box.ConfirmButtonText = "知道了";
        }

        private static void ShowRoomTransition(RoomTransitionReason reason, bool spectator)
        {
            if (reason == RoomTransitionReason.None)
                return;
            foreach (var identifier in new[] { "SpectatorIntro", "InsufficientChips", "LeaveRoom", "TableParticipationError", "RoomTransition" })
                UIManager.Instance.DestroyPanel(identifier);
            UIManager.Instance.PopPanel<RoundResultMsgBox>();
            UIManager.Instance.DestroyPanel(nameof(GameResultMsgBox));
            MsgBox.Create("RoomTransition", box => ConfigureRoomTransition(box, reason, spectator));
        }

        private void InterHandCountdownSyncHandler(InterHandCountdownSync message)
        {
            if (GameController.Available && GameController.Instance.State.LastRoomId == message.RoomId &&
                UIManager.Instance.ContainsPanel<GamePanel>() && GamePanel.Instance.Info.RoomInfo?.Id == message.RoomId &&
                GamePanel.Instance.Info.HandId == message.HandId)
                GamePanel.Instance.SetInterHandCountdown(message.IsActive, message.SecondsRemaining, message.CanStartNextHand);
        }

        private void AccountBalanceSyncHandler(AccountBalanceSync message)
        {
            if (!GameController.Available)
                return;

            GameController.Instance.State.SetChips(message.Chips);
            RoomListPanel.Instance?.RefreshAccountStatus();
            GamePanel.Instance?.SetAccountChips(message.Chips);
        }

        private void ShowInsufficientChipsPrompt(GameInfoSync message)
        {
            MsgBox.Create("InsufficientChips", msgbox =>
            {
                msgbox.Type = MsgboxType.ConfirmAndCancel;
                msgbox.ShowCloseButton = false;
                msgbox.Title = "筹码不足";
                msgbox.Message = $"当前余额为 ${ChipAmountFormatter.Format(message.AccountChips)}，少于本桌大盲 ${ChipAmountFormatter.Format(message.BigBlind)}。你可以继续旁观，或离开房间。";
                msgbox.ConfirmButtonText = "继续旁观";
                msgbox.CancelButtonText = "离开房间";
                msgbox.OnCancel += () => LeaveRoomAfterHand(message.Info.RoomInfo.Id);
            });
        }

        private static void LeaveRoomAfterHand(int roomId)
        {
            (singleton as NetworkRoom)?.RequestLeaveRoom(roomId);
        }

        public bool RequestLeaveRoom(int roomId, bool cancelPending = false, string expectedHandId = null,
            bool? requireImmediate = null)
        {
            if (!NetworkClient.isConnected || NetworkClient.connection == null ||
                !NetworkClient.connection.isAuthenticated)
                return false;

            var panel = UIManager.Instance.ContainsPanel<GamePanel>() ? GamePanel.Instance : null;
            var info = panel != null && panel.Info.RoomInfo?.Id == roomId ? panel.Info : default;
            NetworkClient.Send(new PlayerLeaveRoomRequest
            {
                RoomId = roomId,
                PlayerName = GameController.Instance.State.Nickname,
                CancelPending = cancelPending,
                HandId = expectedHandId ?? info.HandId,
                RequireImmediate = requireImmediate ?? (info.RoomInfo == null || info.IsSpectator || info.Round == GameStage.RoundOver),
            });
            return true;
        }

        private void PlayerLeaveRoomResponseHandler(PlayerLeaveRoomResponse response)
        {
            if (!HasAuthenticatedClient || GameController.Instance.State.LastRoomId != response.RoomId)
                return;

            RoomPanel.Instance?.SetLeaveRequestInFlight(false);
            GamePanel.Instance?.SetLeaveRequestInFlight(false);
            if (response.Status == RoomLeaveStatus.Queued || response.Status == RoomLeaveStatus.Cancelled)
            {
                GamePanel.Instance?.SetLeaveAfterHand(response.Status == RoomLeaveStatus.Queued);
                return;
            }
            if (response.Status == RoomLeaveStatus.Left)
            {
                ClearClientRoomNavigation();
                _lastParticipationStatus = null;
                GameController.Instance.State.SetLastRoom(null);
                OpenLobby();
                ShowRoomTransition(response.TransitionReason, true);
                return;
            }

            if (response.Status == RoomLeaveStatus.StaleRequest)
            {
                MsgBox.Create("LeaveRoomError", box =>
                {
                    box.Title = "牌局状态已变化";
                    box.Message = "这次离开操作已过期，请根据当前界面重新操作。";
                });
                return;
            }

            MsgBox.Create("LeaveRoomError", msgbox =>
            {
                msgbox.Title = "暂时无法离开";
                msgbox.Message = response.Status == RoomLeaveStatus.StorageUnavailable
                    ? "房间状态保存失败，你仍留在当前房间。若已预约离开，将不会加入下一手，请重试。"
                    : response.Status == RoomLeaveStatus.TableFull
                        ? "下一手的名额已被申请，暂时不能取消离开预约。你仍可完成本手。"
                        : "房间关联已发生变化，请等待状态同步后重试。";
                msgbox.ConfirmButtonText = "重试";
                msgbox.OnConfirm += () =>
                {
                    if (GameController.Instance.State.LastRoomId == response.RoomId)
                        RequestLeaveRoom(response.RoomId, response.Status == RoomLeaveStatus.TableFull);
                };
            });
        }

        private void GameResultNotificationHandler(GameResultNotification message)
        {
            if (!HasAuthenticatedClient || !_activeClientRoomId.HasValue ||
                !UIManager.Instance.ContainsPanel<GamePanel>())
                return;
            UIManager.Instance.PushPanel<GameResultMsgBox>(UIManager.Layer.Top);
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
                var sync = new RoomInfoSync() { RoomInfo = PopulateNamecards(room) };
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
            var sync = new RoomInfoSync() { RoomInfo = PopulateNamecards(room) };
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
            else if (targetRoom.IsWaiting && account.Chips < checked((uint)targetRoom.SmallBlindChips * 2U))
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

                response.RoomInfo = PopulateNamecards(room);
                conn.Send(response);
                if (!room.IsWaiting)
                {
                    var playerIndex = _gameManager.FindPlayerSeat(request.RoomId, playerName);
                    var gameInfo = _gameManager.GetGameInfo(request.RoomId);
                    conn.Send(new PlayerStartGameResponse { SuccessCode = 0 });
                    conn.Send(CreateGameInfoSync(gameInfo, playerIndex, playerName));
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
                var status = HandleRoomDeparture(conn, request.RoomId, playerName, false);
                if (status == RoomLeaveStatus.Queued)
                    SendCurrentRoomState(conn, request.RoomId, playerName, reconnectCurrentHand: true);
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
                RoomInfo = PopulateNamecards(room),
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
            conn.Send(CreateGameInfoSync(gameInfo, playerIndex, playerName));
        }

        private void PlayerJoinRoomResponseHandler(PlayerJoinRoomResponse response)
        {
            if (!HasAuthenticatedClient || !_roomEntryPending || !_requestedRoomId.HasValue)
                return;
            var room = response.SuccessCode == 0 ? response.RoomInfo : null;
            if (response.SuccessCode == 0 && (room == null || room.Id != _requestedRoomId ||
                !room.ContainsMember(GameController.Instance.State.UserName)))
                return;
            _roomEntryPending = false;
            _requestedRoomId = null;
            int result = response.SuccessCode;
            switch (result)
            {
                case 0:
                    _activeClientRoomId = room.Id;
                    GameController.Instance.State.SetLastRoom(room.Id);
                    if (room.IsWaiting)
                    {
                        UIManager.Instance.ResetPanels<RoomPanel>();
                        RoomPanel.Instance.RoomInfo = PopulateNamecards(room);
                    }
                    // Active tables open only after their first matching GameInfoSync arrives.
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
            var status = _departures.RequestForHand(message.RoomId, playerName, message.HandId,
                message.RequireImmediate, message.CancelPending);
            if (status == RoomLeaveStatus.StaleRequest)
            {
                conn.Send(new PlayerLeaveRoomResponse { RoomId = message.RoomId, Status = status });
                if (_roomManager.FindRoom(message.RoomId)?.ContainsMember(playerName) == true)
                    BroadcastGameInfo(message.RoomId);
                return;
            }
            SendDepartureResponse(conn, message.RoomId, playerName, status);
        }

        private RoomLeaveStatus HandleRoomDeparture(
            NetworkConnectionToClient conn, int roomId, string playerName, bool cancelPending)
        {
            return SendDepartureResponse(conn, roomId, playerName,
                _departures.Request(roomId, playerName, cancelPending));
        }

        private RoomLeaveStatus SendDepartureResponse(NetworkConnectionToClient conn, int roomId,
            string playerName, RoomLeaveStatus status)
        {
            // A repeated request or a stale recovery bookmark can safely confirm departure,
            // but must never clear an association with a different room.
            if ((status == RoomLeaveStatus.NotMember || status == RoomLeaveStatus.RoomNotFound) &&
                !_roomManager.WhichRoom(playerName, out _))
                status = PersistLastRoom(playerName, null) ? RoomLeaveStatus.Left : RoomLeaveStatus.StorageUnavailable;

            conn.Send(new PlayerLeaveRoomResponse { RoomId = roomId, Status = status });
            if (status == RoomLeaveStatus.Left || status == RoomLeaveStatus.Queued || status == RoomLeaveStatus.Cancelled)
                RefreshRoomAfterDeparture(roomId);
            return status;
        }

        private void CompletePendingDepartures(int roomId)
        {
            foreach (var departure in _departures.CompletePending(roomId))
            {
                _playerManager.GetConnection(departure.PlayerName)?.Send(new PlayerLeaveRoomResponse
                {
                    RoomId = roomId,
                    Status = departure.Status,
                });
            }
        }

        private void RefreshRoomAfterDeparture(int roomId)
        {
            var room = _roomManager.FindRoom(roomId);
            if (room == null)
            {
                _gameManager.CancelGame(roomId, "RoomEmpty");
                return;
            }
            if (!room.IsWaiting)
            {
                if (_gameManager.SynchronizeRoomSeats(roomId) != 0 ||
                    (!_gameManager.IsHandInProgress(roomId) &&
                        _gameManager.GetGameInfo(roomId)?.Stage != GameStage.RoundOver &&
                        _participation.CountNextHandPlayers(roomId) < 2))
                {
                    ReturnRoomToLobby(roomId);
                    return;
                }
            }
            BroadcastRoomInfo(room);
            BroadcastGameInfo(roomId);
        }

        private uint? GetAccountChips(string name)
        {
            try
            {
                return (authenticator as AccountAuthenticator)?.AccountRepository?.FindByUserName(name)?.Chips;
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to load balance for {name}: {exception}");
                return null;
            }
        }

        public bool RequestTableParticipation(GameInfoSync state, TableParticipationAction action)
        {
            if (!NetworkClient.isConnected || NetworkClient.connection == null ||
                !NetworkClient.connection.isAuthenticated)
                return false;
            NetworkClient.Send(new TableParticipationRequest
            {
                RoomId = state.Info.RoomInfo.Id,
                HandId = state.Info.HandId,
                Revision = state.ParticipationRevision,
                Action = action,
                RequireImmediate = state.CanLeaveImmediately,
            });
            return true;
        }

        private void TableParticipationRequestHandler(NetworkConnectionToClient conn, TableParticipationRequest request)
        {
            if (!TryGetAuthenticatedPlayer(conn, out var name))
                return;
            var result = _participation.Request(request.RoomId, name, request.Action, request.HandId, request.Revision,
                request.RequireImmediate);
            conn.Send(new TableParticipationResponse { RoomId = request.RoomId, Result = result });
            if (result == TableParticipationResult.Success)
                RefreshRoomAfterDeparture(request.RoomId);
            else if (_roomManager.FindRoom(request.RoomId)?.ContainsMember(name) == true)
                BroadcastGameInfo(request.RoomId);
        }

        private void TableParticipationResponseHandler(TableParticipationResponse response)
        {
            if (!GameController.Available || GameController.Instance.State.LastRoomId != response.RoomId)
                return;
            GamePanel.Instance?.SetParticipationRequestInFlight(false);
            if (response.Result == TableParticipationResult.Success)
                return;
            MsgBox.Create("TableParticipationError", box =>
            {
                box.Title = "参战状态未变更";
                box.Message = response.Result switch
                {
                    TableParticipationResult.TableFull => "下一手参战名额已满，当前选择保持不变。",
                    TableParticipationResult.InsufficientChips => "筹码至少达到一个大盲后才能申请参战。",
                    TableParticipationResult.StorageUnavailable => "账户暂时不可用，请稍后重试。",
                    _ => "牌局状态已变化，请根据最新界面重新操作。",
                };
            });
        }

        private async UniTask ReturnSpectatorToLobby(int roomId, string name, int serverVersion, RoomTransitionReason reason)
        {
            while (NetworkServer.active && serverVersion == _serverLifecycleVersion &&
                _roomManager.FindRoom(roomId)?.Spectators.Contains(name) == true)
            {
                var status = _departures.Request(roomId, name);
                if (status == RoomLeaveStatus.Left)
                {
                    _playerManager.GetConnection(name)?.Send(new PlayerLeaveRoomResponse
                    {
                        RoomId = roomId, Status = status, TransitionReason = reason,
                    });
                    return;
                }
                await UniTask.Delay(2000, ignoreTimeScale: true);
            }
        }

        private void PlayerOperationRequestHandler(NetworkConnectionToClient conn, PlayerOperationRequest request)
        {
            var response = new PlayerOperationResponse
            {
                Type = request.Type, RoomId = request.RoomId,
                HandId = request.HandId, TurnId = request.TurnId,
            };
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
            var result = _gameManager.ExecuteOperation(request);
            response.SuccessCode = result;
            conn.Send(response);

            // 如果成功，那么就向其他玩家转发新的游戏信息
            if (result != 0)
            {
                conn.Send(CreateGameInfoSync(gameInfo, request.PlayerIndex, playerName));
                return;
            }
            BroadcastGameInfo(request.RoomId);
        }
        private void PlayerOperationResponseHandler(PlayerOperationResponse response)
        {
            var panel = GamePanel.Instance;
            if (panel == null || panel.Info.RoomInfo?.Id != response.RoomId ||
                panel.Info.HandId != response.HandId || panel.Info.TurnId != response.TurnId)
                return;
            if (response.SuccessCode == -8)
            {
                panel.OnOperationRejected();
                return;
            }
            if (response.SuccessCode != 0)
            {
                GamePanel.Instance?.OnOperationRejected();
                var message = response.SuccessCode switch
                {
                    -1 => "房间或牌局已经不存在。",
                    -2 => "服务器无法确认你在本手中的座位。",
                    -3 => "当前还没有轮到你操作。",
                    -7 => "服务器暂时无法保存这次筹码操作，请稍后重试。",
                    _ => response.Type switch
                    {
                        PlayerOperationType.Check => "当前不能过牌，请跟注或弃牌。",
                        PlayerOperationType.Bet when response.SuccessCode == -5 => "下注额没有达到最小加注要求。",
                        PlayerOperationType.Bet when response.SuccessCode == -6 => "之前的短码 all-in 没有重新开放你的加注权。",
                        PlayerOperationType.Bet => "当前筹码或行动状态不允许下注。",
                        PlayerOperationType.Call => "当前不能跟注。",
                        PlayerOperationType.AllIn when response.SuccessCode == -5 => "之前的短码 all-in 没有重新开放你的加注权。",
                        PlayerOperationType.AllIn => "当前不能 all-in。",
                        PlayerOperationType.Fold => "当前不能弃牌。",
                        _ => "服务器拒绝了这次操作。",
                    },
                };
                MsgBox.Create(msgbox =>
                {
                    msgbox.Title = "操作失败";
                    msgbox.Message = message;
                });
                return;
            }

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

            var gameResult = _gameManager.CreateGame(room);
            if (gameResult == 0)
                gameResult = _gameManager.StartNew(room.Id);
            if (gameResult != 0)
            {
                response.SuccessCode = gameResult switch
                {
                    -2 => -7,
                    -8 => -9,
                    _ => -8,
                };
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
            if (!HasAuthenticatedClient || !_activeClientRoomId.HasValue)
                return;
            switch (message.SuccessCode)
            {
                case 0:
                    // GameInfoSync owns this transition and carries the room identity.
                    break;
                case -1:
                    ClearClientRoomNavigation();
                    GameController.Instance.State.SetLastRoom(null);
                    OpenLobby();
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "开始游戏时出错";
                        msgbox.Message = "房间不存在";
                    });

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
                case -7:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "无法开始游戏";
                        msgbox.Message = "至少需要两名在线且拥有一个大盲筹码的玩家";
                    });
                    break;
                case -8:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "无法开始游戏";
                        msgbox.Message = "账户或牌局存储暂时不可用，请稍后重试";
                    });
                    break;
                case -9:
                    MsgBox.Create((msgbox) =>
                    {
                        msgbox.Title = "无法开始游戏";
                        msgbox.Message = "本桌玩家筹码总额超过当前牌局协议可安全处理的上限";
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
                float.IsNaN(message.ThinkingTime) || float.IsInfinity(message.ThinkingTime) ||
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
            sync.RoomInfo = PopulateNamecards(room);
            
            foreach(var player in room.MemberNames)
            {
                var playerConn = _playerManager.GetConnection(player);
                playerConn?.Send(sync);
            }
        }

        private void RoomInfoSyncHandler(RoomInfoSync message)
        {
            if (!HasAuthenticatedClient)
                return;
            var room = message.RoomInfo;
            if (room == null || !room.ContainsMember(GameController.Instance.State.UserName))
                return;
            if (_activeClientRoomId != room.Id)
            {
                // Creating a room is the only unsolicited-looking sync that may establish membership.
                if (!_roomEntryPending || _requestedRoomId.HasValue || _activeClientRoomId.HasValue ||
                    GameController.Instance.State.LastRoomId.HasValue || !room.IsWaiting ||
                    room.OwnerName != GameController.Instance.State.UserName)
                    return;
                _roomEntryPending = false;
                _activeClientRoomId = room.Id;
                GameController.Instance.State.SetLastRoom(room.Id);
            }
            bool enteringWaitingRoom = room.IsWaiting && !UIManager.Instance.ContainsPanel<RoomPanel>();
            if (enteringWaitingRoom)
            {
                UIManager.Instance.ResetPanels<RoomPanel>();
                _lastParticipationStatus = null;
            }
            if (RoomPanel.Instance != null)
                RoomPanel.Instance.RoomInfo = PopulateNamecards(room);
            if (enteringWaitingRoom)
                ShowRoomTransition(message.TransitionReason, false);
        }

        private void RoomListRequestHandler(NetworkConnectionToClient conn, RoomListRequest request)
        {
            RoomListResponse response = new RoomListResponse();
            response.RoomList = _roomManager.RoomList;
            conn.Send(response);
        }

        private void RoomListResponseHandler(RoomListResponse response)
        {
            if (RoomListPanel.Instance != null)
                RoomListPanel.Instance.Rooms = response.RoomList;
        }


        private void RoundResultNotificationHandler(RoundResultNotification message)
        {
            if (!HasAuthenticatedClient || _activeClientRoomId != message.RoomId ||
                !UIManager.Instance.ContainsPanel<GamePanel>())
                return;
            UIManager.Instance.PushPanel<RoundResultMsgBox>(UIManager.Layer.System);
            RoundResultMsgBox.Instance.Info = message.RoundResult;
            GameAudioManager.EnsureClient()?.ShowResult(message.RoomId, GamePanel.Instance.Info.HandId,
                GameController.Instance.State.UserName, RoundResultMsgBox.Instance.Info);
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
            int serverVersion = _serverLifecycleVersion;
            var gameInfo = _gameManager.GetGameInfo(roomId);
            if (gameInfo == null)
                return;
            gameInfo.OperatingPlayer = -1;
            OnGameInfoChanged(roomId);
            // 等待一段时间
            await UniTask.WaitForSeconds(2);
            if (NetworkServer.active && !_serverStorageFailed && serverVersion == _serverLifecycleVersion &&
                ReferenceEquals(_gameManager.GetGameInfo(roomId), gameInfo) &&
                gameInfo.Stage != GameStage.RoundOver && gameInfo.Stage != GameStage.GameOver &&
                _gameManager.NextStage(roomId) == -7)
            {
                HandleFatalStorageError(new InvalidOperationException("Hand settlement could not be saved."));
            }
        }

        private async void OnRoomRoundEnd(int roomId, RoundResult result)
        {
            int serverVersion = _serverLifecycleVersion;
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
                    RoomId = roomId,
                    RoundResult = room.Spectators.Contains(name) ? result.ForSpectator() : result
                };
                conn?.Send(roundResult);
                SendAccountBalance(conn, name);
            }

            gameInfo.Timer.Stop();
            if (_roomManager.FindRoom(roomId) == null)
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
            BroadcastGameInfo(roomId);
            var settledGame = gameInfo;
            var settledHandId = gameInfo.HandId;
            for (int secondsRemaining = 10; secondsRemaining > 0; secondsRemaining--)
            {
                room = _roomManager.FindRoom(roomId);
                gameInfo = _gameManager.GetGameInfo(roomId);
                if (!NetworkServer.active || _serverStorageFailed || serverVersion != _serverLifecycleVersion ||
                    room == null || !ReferenceEquals(gameInfo, settledGame) ||
                    gameInfo.HandId != settledHandId || room.IsWaiting || gameInfo.Stage != GameStage.RoundOver)
                    return;

                BroadcastInterHandCountdown(roomId, true, secondsRemaining);
                await UniTask.Delay(1000, ignoreTimeScale: true);
            }

            room = _roomManager.FindRoom(roomId);
            gameInfo = _gameManager.GetGameInfo(roomId);
            if (!NetworkServer.active || _serverStorageFailed || serverVersion != _serverLifecycleVersion ||
                    room == null || !ReferenceEquals(gameInfo, settledGame) ||
                    gameInfo.HandId != settledHandId || room.IsWaiting || gameInfo.Stage != GameStage.RoundOver)
                return;

            CompletePendingDepartures(roomId);
            if (_roomManager.FindRoom(roomId) == null)
            {
                _gameManager.CancelGame(roomId, "RoomEmpty");
                return;
            }
            _gameManager.PrepareNextHandSeats(roomId);
            _roomManager.ReleaseWatchingSeats(roomId);
            if (_participation.CountNextHandPlayers(roomId) < 2)
            {
                ReturnRoomToLobby(roomId);
                return;
            }
            _participation.AdmitForNextHand(roomId);
            if (_gameManager.SynchronizeRoomSeats(roomId) != 0 || _gameManager.StartNew(roomId) != 0)
            {
                ReturnRoomToLobby(roomId);
                return;
            }

            BroadcastInterHandCountdown(roomId, false, 0);
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

            var sync = new RoomInfoSync { RoomInfo = PopulateNamecards(room) };
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
                conn?.Send(CreateGameInfoSync(gameInfo, seatIndex, player.PlayerName));
            }

            var room = _roomManager.FindRoom(roomId);
            if (room == null)
                return;
            foreach (var spectatorName in room.Spectators)
            {
                _playerManager.GetConnection(spectatorName)?
                    .Send(CreateGameInfoSync(gameInfo, -1, spectatorName));
            }
        }

        private GameInfoSync CreateGameInfoSync(
            ServerGameInfo gameInfo,
            int playerIndex,
            string playerName)
        {
            PopulateNamecards(gameInfo.RoomInfo);
            var bigBlind = checked((uint)gameInfo.RoomInfo.SmallBlindChips * 2U);
            uint accountChips = 0;
            var participationStatus = TableParticipationStatus.Spectating;
            if (playerIndex >= 0 && playerIndex < gameInfo.PlayersCount)
            {
                var player = gameInfo.Players[playerIndex];
                accountChips = player.AccountChips;
                participationStatus = player.IsWaitingForNextHand || !player.ParticipatedThisHand
                    ? TableParticipationStatus.WaitingForNextHand
                    : TableParticipationStatus.Playing;
            }
            else
            {
                var balanceLoaded = false;
                try
                {
                    var accountRepository = (authenticator as AccountAuthenticator)?.AccountRepository;
                    var account = accountRepository?.FindByUserName(playerName);
                    if (account != null)
                    {
                        accountChips = account.Chips;
                        balanceLoaded = true;
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError($"Unable to load spectator balance for {playerName}: {exception}");
                }

                participationStatus = balanceLoaded && accountChips < bigBlind
                    ? TableParticipationStatus.InsufficientChips
                    : gameInfo.RoomInfo.NextHandPlayers.Contains(playerName)
                        ? TableParticipationStatus.WaitingForNextHand
                        : TableParticipationStatus.Spectating;
            }

            return new GameInfoSync
            {
                Info = new ClientGameInfo(gameInfo, playerIndex),
                AccountChips = accountChips,
                BigBlind = bigBlind,
                ParticipationStatus = participationStatus,
                CanLeaveImmediately = !_gameManager.RequiresDeferredLeave(gameInfo.RoomInfo.Id, playerName),
                LeaveAfterHand = gameInfo.RoomInfo.FindSeat(playerName)?.LeaveAfterHand == true,
                SitOutAfterHand = gameInfo.RoomInfo.FindSeat(playerName)?.SitOutAfterHand == true,
                JoinNextHand = gameInfo.RoomInfo.NextHandPlayers.Contains(playerName),
                CanJoinNextHand = _participation.CanJoin(gameInfo.RoomInfo.Id, playerName) == TableParticipationResult.Success,
                ParticipationRevision = _participation.GetRevision(playerName),
            };
        }

        private void BroadcastInterHandCountdown(int roomId, bool isActive, int secondsRemaining)
        {
            var room = _roomManager.FindRoom(roomId);
            if (room == null)
                return;

            var message = new InterHandCountdownSync
            {
                RoomId = roomId,
                IsActive = isActive,
                SecondsRemaining = secondsRemaining,
                HandId = _gameManager.GetGameInfo(roomId)?.HandId,
                CanStartNextHand = _participation.CountNextHandPlayers(roomId) >= 2,
            };
            foreach (var playerName in room.MemberNames)
                _playerManager.GetConnection(playerName)?.Send(message);
        }

        private void SendAccountBalance(NetworkConnectionToClient conn, string playerName)
        {
            if (conn == null)
                return;

            try
            {
                var accountRepository = (authenticator as AccountAuthenticator)?.AccountRepository;
                var account = accountRepository?.FindByUserName(playerName);
                if (account != null)
                {
                    if (_playerManager.TryGetSession(conn, out var session) && session.Account.AccountId == account.AccountId)
                        session.Account = account.Copy();
                    conn.Send(new AccountBalanceSync { Chips = account.Chips });
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to synchronize account balance for {playerName}: {exception}");
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

            BroadcastInterHandCountdown(roomId, false, 0);
            _roomManager.ReleaseWatchingSeats(roomId);
            var reason = _participation.CountNextHandPlayers(roomId) < 2
                ? RoomTransitionReason.InsufficientPlayers : RoomTransitionReason.GameStopped;
            if (_serverStorageFailed)
                return;
            _roomManager.ReturnToWaiting(roomId);
            _gameManager.CancelGame(roomId, "ReturnedToLobby");
            foreach (var name in room.Spectators.ToArray())
                ReturnSpectatorToLobby(roomId, name, _serverLifecycleVersion, reason).Forget();
            if (_roomManager.FindRoom(roomId) != null)
            {
                var message = new RoomInfoSync { RoomInfo = PopulateNamecards(room), TransitionReason = reason };
                foreach (var name in room.Players)
                    _playerManager.GetConnection(name)?.Send(message);
            }
        }
        #endregion
    }
}


