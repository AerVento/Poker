using Assets.Scripts.Network.Messages;
using Game.Network.Accounts;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;

namespace Game.Network.Diagnostics
{
    public sealed class PokerIntegrationRunner : MonoBehaviour
    {
        private const string Password = "Integration123!";
        private const float TimeoutSeconds = 120f;

        private NetworkRoom _networkRoom;
        private AccountAuthenticator _authenticator;
        private string _role;
        private string _runId;
        private string _outputDirectory;
        private int _clientIndex = -1;
        private string _userName;
        private int _roomId = -1;
        private int _roundsCompleted;
        private bool _authenticated;
        private bool _joined;
        private bool _readySent;
        private bool _startSent;
        private bool _actionPending;
        private bool _sawCountdown;
        private bool _sawFirstHandWaiting;
        private bool _sawSecondHandPlaying;
        private bool _finishing;
        private bool _sawRiver;
        private bool _sawBet;
        private GameInfoSync? _latestGameInfo;
        private bool _testDepartures;
        private bool _queueRequested;
        private bool _leaveQueued;
        private bool _cancelRequested;
        private bool _leaveCancelled;
        private bool _secondQueueRequested;
        private bool _leaveCompleted;
        private bool _waitingLeaveCompleted;
        private bool _betweenHandLeaveCompleted;
        private bool _reconnecting;
        private bool _recoveryRequested;
        private bool _recoveredPending;
        private bool _sawDisconnectedTitle;
        private int _waitingLeavePhase;
        private bool _testSpectators;
        private bool _testStorage;
        private bool _storageAllIn;
        private bool _storageNotice;
        private bool _storageFaultArmed;
        private int _spectatorPhase;
        private bool _spectatorFullDenied;
        private bool _sawSitOut;
        private bool _sawWaitingRoom;
        private int _observerSnapshots;
        private bool _sitOutRequested;
        private bool _roomSettingsSent;
        private float _nextLobbyRefresh;
        private bool _confirmationChecked;
        private bool _introChecked;

        public static bool TryStart(GameController controller)
        {
            var role = GetArgument("-pokerIntegrationRole");
            if (role != "server" && role != "client" && role != "recovery")
                return false;

            var runId = GetArgument("-pokerIntegrationRun");
            var outputDirectory = GetArgument("-pokerIntegrationOutput");
            if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(outputDirectory))
                throw new InvalidOperationException("Integration run and output arguments are required.");

            var runner = controller.gameObject.AddComponent<PokerIntegrationRunner>();
            runner._role = role;
            runner._testDepartures = GetArgument("-pokerIntegrationScenario") == "departures";
            runner._testSpectators = GetArgument("-pokerIntegrationScenario") == "spectators";
            runner._testStorage = (GetArgument("-pokerIntegrationScenario") ?? "").StartsWith("storage-");
            runner._storageAllIn = GetArgument("-pokerIntegrationScenario") == "storage-allin";
            runner._runId = SanitizeFileName(runId);
            runner._outputDirectory = Path.GetFullPath(outputDirectory);
            if (role == "client" && !int.TryParse(GetArgument("-pokerIntegrationClient"), out runner._clientIndex))
                throw new InvalidOperationException("An integration client index is required.");
            return true;
        }

        private void Start()
        {
            Directory.CreateDirectory(_outputDirectory);
            _networkRoom = NetworkManager.singleton as NetworkRoom;
            _authenticator = _networkRoom == null
                ? null
                : _networkRoom.GetComponent<AccountAuthenticator>();
            if (_networkRoom == null || _authenticator == null)
            {
                Fail("NetworkRoom or AccountAuthenticator is missing.");
                return;
            }

            if (_role == "recovery")
                RunStorageRecovery();
            else if (_role == "server")
                StartCoroutine(RunServer());
            else
                StartCoroutine(RunClient());
        }

        private IEnumerator RunServer()
        {
            _authenticator.ConfigureDatabaseFileName($"PokerIntegration-{_runId}.db");
            _networkRoom.StartServer();
            if (_testStorage)
            {
#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
                var raw = (_authenticator.AccountRepository as GuardedAccountRepository)?.InnerRepository as SqliteAccountRepository;
                Application.quitting += () =>
                {
                    if (!_networkRoom.IsServerShuttingDown || NetworkServer.active || !_storageFaultArmed)
                    {
                        WriteResult("server.fail", "Server did not stop in response to the injected database error.");
                        return;
                    }
                    var report = raw.ReconcileChips();
                    WriteResult(report.IsConsistent && report.ActiveHandCount == 1 ? "server.pass" : "server.fail",
                        JsonSerializer.Serialize(new { Stopped = !NetworkServer.active, FatalStorage = true, Report = report }));
                };
#endif
            }
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var failure = Enumerable.Range(0, 3)
                    .Select(index => ResultPath(index, "fail"))
                    .FirstOrDefault(File.Exists);
                if (failure != null)
                {
                    Fail("A client failed: " + File.ReadAllText(failure));
                    yield break;
                }

                if (_testStorage && !_storageFaultArmed && File.Exists(Path.Combine(_outputDirectory, "storage-first-hand.finished")))
                {
                    ExecuteStorageTestSql("CREATE TRIGGER integration_fail_settlement BEFORE UPDATE OF chips ON player_accounts WHEN NEW.chips > OLD.chips BEGIN SELECT RAISE(ABORT, 'Injected settlement write failure'); END;");
                    _storageFaultArmed = true;
                    WriteResult("storage-fault.armed", "armed");
                }
                if (!_testStorage && Enumerable.Range(0, 3).All(index => File.Exists(ResultPath(index, "pass"))))
                {
#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
                    var repository = (_authenticator.AccountRepository as GuardedAccountRepository)?.InnerRepository as SqliteAccountRepository;
                    var report = repository?.ReconcileChips();
                    if (report == null || !report.IsConsistent)
                    {
                        Fail("Final chip reconciliation failed: " + JsonSerializer.Serialize(report));
                        yield break;
                    }
                    if (report.ActiveHandCount != 0 || report.AccountBalanceTotal != 30000)
                    {
                        Fail("Unexpected final chip totals: " + JsonSerializer.Serialize(report));
                        yield break;
                    }

                    if ((_testDepartures || _testSpectators) && Enumerable.Range(0, 3).Any(index =>
                        repository.FindByUserName($"Smoke{_runId.Substring(0, Math.Min(8, _runId.Length))}_{index}")?.LastRoomId != null))
                    {
                        Fail("A departed account retained a last-room association.");
                        yield break;
                    }
                    _networkRoom.StopServer();
                    WriteResult("server.pass", JsonSerializer.Serialize(report));
                    Application.Quit(0);
                    yield break;
#else
                    Fail("This build target does not include the server account database.");
                    yield break;
#endif
                }

                yield return new WaitForSecondsRealtime(0.1f);
            }

            Fail("Timed out waiting for integration clients.");
        }

        private void ExecuteStorageTestSql(string sql)
        {
#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
            var path = Path.Combine(Application.persistentDataPath, $"PokerIntegration-{_runId}.db");
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + path);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
#endif
        }

        private void RunStorageRecovery()
        {
#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
            ExecuteStorageTestSql("DROP TRIGGER integration_fail_settlement;");
            var path = Path.Combine(Application.persistentDataPath, $"PokerIntegration-{_runId}.db");
            var repository = new SqliteAccountRepository(path);
            repository.Initialize();
            var report = repository.ReconcileChips();
            repository.Initialize();
            if (!report.IsConsistent || report.ActiveHandCount != 0 || report.AccountBalanceTotal != 30000 ||
                report.LedgerEntryCount != repository.ReconcileChips().LedgerEntryCount)
            {
                Fail("Storage restart recovery failed: " + JsonSerializer.Serialize(report));
                return;
            }
            WriteResult("recovery.pass", JsonSerializer.Serialize(report));
            Application.Quit(0);
#endif
        }

        private IEnumerator FinishStorageClient()
        {
            _finishing = true;
            yield return new WaitForSecondsRealtime(0.2f);
            var box = FindObjectsOfType<UI.MsgBox>().FirstOrDefault(item => item.Title == "服务器已停止服务");
            if (!_storageNotice || NetworkClient.isConnected || _roundsCompleted != 1 ||
                UI.TitlePanel.Instance == null || !UI.TitlePanel.Instance.gameObject.activeInHierarchy ||
                (UI.GamePanel.Instance != null && UI.GamePanel.Instance.gameObject.activeInHierarchy) ||
                Framework.UI.UIManager.Instance.ContainsPanel<UI.GamePanel>() ||
                box == null || !box.Message.Contains("数据保存失败") ||
                FindObjectsOfType<UI.MsgBox>().Any(item => item.Title == "与服务器断开连接"))
            {
                Fail("Shutdown did not show its storage reason on the title screen after one successful hand: " +
                    JsonSerializer.Serialize(new
                    {
                        _storageNotice, NetworkClient.isConnected, _roundsCompleted,
                        TitleActive = UI.TitlePanel.Instance != null && UI.TitlePanel.Instance.gameObject.activeInHierarchy,
                        GameActive = UI.GamePanel.Instance != null && UI.GamePanel.Instance.gameObject.activeInHierarchy,
                        Order = Framework.UI.UIManager.Instance.PanelOrder,
                        Message = box == null ? null : box.Message,
                    }));
                yield break;
            }
            WriteResult(ResultPath(_clientIndex, "pass"), JsonSerializer.Serialize(new
            {
                Client = _clientIndex, SuccessfulHands = _roundsCompleted, StorageReasonReceived = _storageNotice,
                ReturnedToTitle = true, Message = box.Message, ShippingUIButtons = true,
            }));
            Application.Quit(0);
        }

        private IEnumerator RunClient()
        {
            if (_clientIndex < 0 || _clientIndex > 2)
            {
                Fail("Client index must be between 0 and 2.");
                yield break;
            }

            LoginCredentialStore.Clear();
            var userNameRunId = _runId.Length <= 8 ? _runId : _runId.Substring(0, 8);
            _userName = $"Smoke{userNameRunId}_{_clientIndex}";
            _networkRoom.networkAddress = "127.0.0.1";
            _authenticator.AuthenticationResponseReceived += OnAuthenticationResponse;
            _networkRoom.StartClient();
            RegisterClientHandlers();
            _authenticator.Register(_userName, Password);

            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!_finishing && Time.realtimeSinceStartup < deadline)
            {
                if (_authenticated && NetworkClient.connection != null && NetworkClient.connection.isAuthenticated)
                {
                    if (_clientIndex == 0 && _roomId == -1)
                    {
                        Click(UI.RoomListPanel.Instance, "_createRoom");
                        _roomId = -2;
                    }
                    else if (_clientIndex == 1 && !_joined && TryReadRoomId(out var roomId))
                    {
                        SendJoin(roomId);
                    }
                    else if (_clientIndex == 2 && !_joined &&
                        File.Exists(Path.Combine(_outputDirectory, "hand-1.started")) &&
                        TryReadRoomId(out roomId))
                    {
                        SendJoin(roomId);
                    }
                }

                if (_testDepartures && _clientIndex == 0 && _leaveCancelled &&
                    File.Exists(Path.Combine(_outputDirectory, "waiting-rejoined")))
                    File.WriteAllText(Path.Combine(_outputDirectory, "departure-preflight.ready"), "ready");
                if (_latestGameInfo.HasValue)
                    TryOperate(_latestGameInfo.Value);
                yield return new WaitForSecondsRealtime(0.05f);
            }

            if (!_finishing)
                Fail("Timed out before completing the scripted client flow.");
        }

        private void RegisterClientHandlers()
        {
            if (_testStorage)
                RegisterClientHandler<ServerShutdownNotice>("ServerShutdownNoticeHandler", notice =>
                {
                    _storageNotice = notice.Reason == ServerShutdownReason.StorageFailure;
                    StartCoroutine(FinishStorageClient());
                });
            RegisterClientHandler<PlayerLeaveRoomResponse>("PlayerLeaveRoomResponseHandler", OnLeaveRoomResponse);
            RegisterClientHandler<RoomInfoSync>("RoomInfoSyncHandler", OnRoomInfoSync);
            RegisterClientHandler<PlayerJoinRoomResponse>("PlayerJoinRoomResponseHandler", OnJoinRoomResponse);
            RegisterClientHandler<PlayerStartGameResponse>("PlayerStartGameResponseHandler", OnStartGameResponse);
            RegisterClientHandler<GameInfoSync>("GameInfoSyncHandler", OnGameInfoSync);
            RegisterClientHandler<TableParticipationResponse>("TableParticipationResponseHandler", response =>
            {
                if (_testSpectators && response.Result == TableParticipationResult.TableFull && _spectatorPhase == 2)
                {
                    _spectatorFullDenied = true;
                    foreach (var box in FindObjectsOfType<UI.MsgBox>())
                        if (box.Title == "参战状态未变更") Click(box, "_confirmButton");
                }
                else if (response.Result != TableParticipationResult.Success)
                    Fail("Unexpected participation result: " + response.Result);
            });
            RegisterClientHandler<PlayerOperationResponse>("PlayerOperationResponseHandler", OnOperationResponse);
            RegisterClientHandler<RoundResultNotification>("RoundResultNotificationHandler", OnRoundResult);
            RegisterClientHandler<InterHandCountdownSync>("InterHandCountdownSyncHandler", OnCountdown);

        }

        private void RegisterClientHandler<T>(string handlerName, Action<T> inspect) where T : struct, NetworkMessage
        {
            // Exercise the shipping UI handlers as well as the scripted network assertions.
            var handler = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), _networkRoom, handlerName);
            NetworkClient.RegisterHandler<T>(message =>
            {
                handler(message);
                inspect(message);
            }, false);
        }

        private void OnAuthenticationResponse(AccountAuthenticationResponse response)
        {
            if (response.Code != AccountAuthenticationCode.Success)
            {
                Fail("Authentication failed with " + response.Code + ".");
                return;
            }

            if (response.UserName != _userName)
            {
                Fail("Authentication restored a different window's account.");
                return;
            }
            _authenticated = true;
            if (_reconnecting)
            {
                if (!response.HasLastRoom)
                {
                    Fail("Pending departure lost its room before settlement.");
                    return;
                }
                StartCoroutine(RecoverPendingRoom());
            }
        }

        private void OnRoomInfoSync(RoomInfoSync message)
        {
            var room = message.RoomInfo;
            if (room == null)
                return;

            _roomId = room.Id;
            _joined = room.ContainsMember(_userName);
            if (_testSpectators && room.IsWaiting && _roundsCompleted >= 3)
            {
                if (_clientIndex != 1 || !UI.RoomPanel.Instance.gameObject.activeInHierarchy ||
                    room.PlayerCount != 1 || room.Spectators.Count != 0)
                {
                    Fail("Insufficient players did not leave one participant in RoomPanel and evict every observer.");
                    return;
                }
                if (message.TransitionReason != RoomTransitionReason.InsufficientPlayers ||
                    !AcknowledgeTransition(false)) return;
                _sawWaitingRoom = true;
                Click(UI.RoomPanel.Instance, "_back");
                return;
            }
            if (_testSpectators && _clientIndex == 0 && !_startSent && room.MaxPlayerCount != 2)
            {
                if (!_roomSettingsSent)
                {
                    _roomSettingsSent = true;
                    NetworkClient.Send(new RoomInfoChange
                    {
                        RoomId = room.Id, MaxPlayer = 2, SmallBlindChips = 1, ThinkingTime = 120,
                    });
                }
                return;
            }
            if (_clientIndex == 0)
            {
                File.WriteAllText(Path.Combine(_outputDirectory, "room.id"), room.Id.ToString());
                var otherPlayersReady = room.Seats
                    .Where(seat => seat.PlayerName != _userName)
                    .All(seat => seat.IsReady);
                if (!_startSent && room.PlayerCount >= 2 && otherPlayersReady)
                {
                    _startSent = true;
                    Click(UI.RoomPanel.Instance, "_start");
                }
            }
        }

        private void OnJoinRoomResponse(PlayerJoinRoomResponse response)
        {
            if (response.SuccessCode != 0)
            {
                Fail("Join room failed with " + response.SuccessCode + ".");
                return;
            }

            var room = response.RoomInfo;
            _roomId = room.Id;
            _joined = true;
            if (_clientIndex == 1 && !_readySent)
            {
                _readySent = true;
                Click(UI.RoomPanel.Instance, "_ready");
            }
            if (_clientIndex == 2)
            {
                File.WriteAllText(Path.Combine(_outputDirectory, "client-2.joined"), "joined");
                if (_testDepartures && _waitingLeavePhase == 1)
                    File.WriteAllText(Path.Combine(_outputDirectory, "waiting-rejoined"), "joined");
            }
        }

        private void OnStartGameResponse(PlayerStartGameResponse response)
        {
            if (response.SuccessCode != 0)
                Fail("Start game failed with " + response.SuccessCode + ".");
        }

        private void OnGameInfoSync(GameInfoSync message)
        {
            var info = message.Info;
            if (info.RoomInfo == null)
                return;

            if (_testDepartures)
            {
                var panel = UI.GamePanel.Instance;
                var leaveButton = panel == null ? null : panel.transform.Find("LeaveButton");
                var label = leaveButton == null ? null : leaveButton.GetComponentInChildren<TMPro.TMP_Text>();
                var expectedLabel = message.LeaveAfterHand ? "取消离开" :
                    message.CanLeaveImmediately ? "离开房间" : "本手后离开";
                if (panel == null || !panel.gameObject.activeInHierarchy || label == null || label.text != expectedLabel)
                {
                    Fail("Built game panel is missing the synchronized departure control.");
                    return;
                }
            }

            var intro = FindObjectsOfType<UI.MsgBox>().FirstOrDefault(box => box.Title == "你正在旁观");
            if (intro != null)
            {
                if (_introChecked || !info.IsSpectator || !intro.Message.Contains("下一手参战"))
                    Fail("Spectator introduction repeated or appeared for an active player.");
                _introChecked = true;
                Click(intro, "_confirmButton");
                WriteResult($"client-{_clientIndex}.intro.checked", "First observer introduction acknowledged");
            }
            _latestGameInfo = message;
            _roomId = info.RoomInfo.Id;
            _sawRiver |= info.Round == GameStage.River;
            _sawBet |= info.Players.Any(player => player.LastOperation != null &&
                (player.LastOperation.Type == PlayerOperationType.Bet || player.LastOperation.Type == PlayerOperationType.Raise));
            if (_roundsCompleted == 0 && _clientIndex == 0)
                File.WriteAllText(Path.Combine(_outputDirectory, "hand-1.started"), "started");

            if (_clientIndex == 2 && _roundsCompleted == 0 &&
                message.ParticipationStatus == TableParticipationStatus.WaitingForNextHand &&
                info.Hole.Count == 0)
            {
                _sawFirstHandWaiting = true;
            }
            if (_clientIndex == 2 && _roundsCompleted >= 1 &&
                message.ParticipationStatus == TableParticipationStatus.Playing &&
                info.Hole.Count == 2)
            {
                _sawSecondHandPlaying = true;
            }

            if (_testSpectators)
                InspectSpectatorState(message);
            else if (_clientIndex == 2 && info.IsSpectator && message.CanJoinNextHand && !message.JoinNextHand &&
                Field<UnityEngine.UI.Button>(UI.GamePanel.Instance, "_participationButton").interactable)
                Click(UI.GamePanel.Instance, "_participationButton");
            if (_testDepartures)
                InspectDepartureState(message);
            TryOperate(message);
        }

        private void InspectDepartureState(GameInfoSync message)
        {
            var info = message.Info;
            if (_clientIndex == 2 && _roundsCompleted == 0 && _waitingLeavePhase == 0 &&
                message.ParticipationStatus == TableParticipationStatus.WaitingForNextHand)
            {
                if (!message.CanLeaveImmediately || info.Hole.Count != 0)
                {
                    Fail("Mid-hand joiner must be able to leave without private cards.");
                    return;
                }
                _waitingLeavePhase = -1;
                SendLeave(false);
            }
            if (_clientIndex != 0 || info.Round >= GameStage.RoundOver)
                return;
            if (_roundsCompleted == 0 && !_queueRequested)
            {
                _queueRequested = true;
                if (message.CanLeaveImmediately)
                {
                    Fail("Current hand participant can leave immediately.");
                    return;
                }
                SendLeave(false);
            }
            else if (_roundsCompleted == 0 && _leaveQueued && message.LeaveAfterHand && !_cancelRequested)
            {
                _cancelRequested = true;
                if (info.Hole.Count != 2 || info.CurrentPlayer.IsLeaving || info.CurrentPlayer.IsFold)
                {
                    Fail("Queueing departure changed current-hand rights.");
                    return;
                }
                SendLeave(true);
            }
            else if (_roundsCompleted == 1 && !_secondQueueRequested)
            {
                _secondQueueRequested = true;
                SendLeave(false);
            }
            else if (_roundsCompleted == 1 && message.LeaveAfterHand && !_reconnecting && !_recoveredPending)
            {
                _reconnecting = true;
                StartCoroutine(ReconnectWithPendingDeparture());
            }
            else if (_recoveryRequested && message.LeaveAfterHand && !_recoveredPending)
            {
                if (message.CanLeaveImmediately || info.Hole.Count != 2)
                {
                    Fail("Reconnect changed pending hand participation.");
                    return;
                }
                _recoveredPending = true;
                _reconnecting = false;
                File.WriteAllText(Path.Combine(_outputDirectory, "pending-reconnected"), "recovered");
            }
        }

        private void SendLeave(bool cancelPending)
        {
            NetworkClient.Send(new PlayerLeaveRoomRequest
            {
                RoomId = _roomId,
                // Deliberately wrong: the server must use the authenticated identity.
                PlayerName = _testDepartures ? "NotTheAuthenticatedPlayer" : _userName,
                CancelPending = cancelPending,
            });
        }

        private void OnLeaveRoomResponse(PlayerLeaveRoomResponse response)
        {
            if (response.Status == RoomLeaveStatus.Queued)
            {
                _leaveQueued = true;
                return;
            }
            if (response.Status == RoomLeaveStatus.Cancelled)
            {
                _leaveCancelled = true;
                return;
            }
            if (response.Status != RoomLeaveStatus.Left)
            {
                Fail("Unexpected departure response: " + response.Status);
                return;
            }
            if (GameController.Instance.State.LastRoomId.HasValue ||
                !UI.RoomListPanel.Instance.gameObject.activeInHierarchy ||
                (UI.GamePanel.Instance != null && UI.GamePanel.Instance.gameObject.activeInHierarchy))
            {
                Fail("Departure acknowledgement did not return the client to the lobby.");
                return;
            }
            if (response.TransitionReason != RoomTransitionReason.None && !AcknowledgeTransition(true))
                return;
            _latestGameInfo = null;
            if (_testSpectators)
            {
                if (_clientIndex == 2 && _spectatorPhase == 3)
                {
                    _spectatorPhase = 4;
                    _joined = false;
                    _waitingLeaveCompleted = true;
                    return;
                }
                _leaveCompleted = true;
                StartCoroutine(FinishSpectatorClient());
                return;
            }
            if (_testDepartures && _clientIndex == 2 && _waitingLeavePhase == -1)
            {
                _waitingLeaveCompleted = true;
                _waitingLeavePhase = 1;
                _joined = false;
                return;
            }
            if (_testDepartures && _clientIndex == 2 && _waitingLeavePhase == 2)
            {
                _betweenHandLeaveCompleted = true;
                _waitingLeavePhase = 3;
                _joined = false;
                return;
            }
            _leaveCompleted = true;
        }

        private IEnumerator ReconnectWithPendingDeparture()
        {
            _authenticated = false;
            _latestGameInfo = null;
            _networkRoom.StopClient();
            yield return new WaitForSecondsRealtime(0.5f);
            _sawDisconnectedTitle = UI.TitlePanel.Instance != null &&
                UI.TitlePanel.Instance.gameObject.activeInHierarchy;
            var box = FindObjectsOfType<UI.MsgBox>().FirstOrDefault(item => item.Title == "与服务器断开连接");
            if (box == null || box.ConfirmButtonText != "重新登录")
            {
                Fail("Disconnect did not provide the shipping login action.");
                yield break;
            }
            Click(box, "_confirmButton");
            if (UI.LoginPanel.Instance == null || !UI.LoginPanel.Instance.gameObject.activeInHierarchy)
            {
                Fail("Reconnect button did not open LoginPanel.");
                yield break;
            }
            RegisterClientHandlers();
            WriteResult("relogin-button.checked", "Shipping reconnect button opened saved login");
        }

        private IEnumerator RecoverPendingRoom()
        {
            yield return null;
            _recoveryRequested = true;
            var box = Framework.UI.UIManager.Instance.GetPanel<UI.MsgBox>("RoomRecovery");
            Click(box, "_confirmButton");
        }

        private void TryOperate(GameInfoSync message)
        {
            var info = message.Info;
            if (_finishing || info.Round >= GameStage.RoundOver)
                return;
            if (_testSpectators)
            {
                var marker = _roundsCompleted == 0 ? "observer-preflight" :
                    _roundsCompleted == 1 ? "next-hand-reserved" : "third-hand-sitout";
                if (!File.Exists(Path.Combine(_outputDirectory, marker)))
                    return;
            }
            if (_testDepartures &&
                ((_roundsCompleted == 0 && !File.Exists(Path.Combine(_outputDirectory, "departure-preflight.ready"))) ||
                (_roundsCompleted == 1 && !File.Exists(Path.Combine(_outputDirectory, "pending-reconnected")))))
                return;
            // A join notification can arrive before the other process writes its marker.
            // Retry the latest snapshot from RunClient rather than waiting for a new sync.
            if (_roundsCompleted == 0 &&
                !File.Exists(Path.Combine(_outputDirectory, "client-2.joined")))
            {
                return;
            }
            if (_actionPending ||
                message.ParticipationStatus != TableParticipationStatus.Playing ||
                info.CurrentPlayerIndex < 0 ||
                info.CurrentPlayerIndex != info.OperatingPlayer)
            {
                return;
            }

            if (_testStorage && _roundsCompleted > 0 && !File.Exists(Path.Combine(_outputDirectory, "storage-fault.armed")))
                return;
            var operation = _testStorage && _storageAllIn && _roundsCompleted > 0
                ? PlayerOperationType.AllIn : PlayerOperationType.Fold;
            uint amount = 0;
            if (_roundsCompleted == 0)
            {
                if (info.Round == GameStage.Flop && info.CurrentHighestPlayerBet == 0 && info.CanRaise)
                {
                    operation = PlayerOperationType.Bet;
                    amount = info.LeastRaise;
                }
                else
                {
                    operation = info.CurrentPlayer.StageBet < info.CurrentHighestPlayerBet
                        ? PlayerOperationType.Call : PlayerOperationType.Check;
                }
            }
            if ((_testSpectators || _testStorage) && _clientIndex == 0 && !_confirmationChecked)
            {
                CheckOperationConfirmations(info);
                if (_finishing) return;
            }
            _actionPending = true;
            if (_testSpectators || _testStorage)
            {
                var area = Field<UI.GamePanelElement.OperationArea>(UI.GamePanel.Instance, "_operationArea");
                if (operation == PlayerOperationType.Bet)
                    Field<TMPro.TMP_InputField>(area, "_betInput").text = amount.ToString();
                var button = operation == PlayerOperationType.Bet ? "_betOrRaiseButton" :
                    operation == PlayerOperationType.Check ? "_checkButton" :
                    operation == PlayerOperationType.Call ? "_callButton" :
                    operation == PlayerOperationType.AllIn ? "_allInButton" : "_foldButton";
                Click(area, button);
                var confirmation = Field<UI.MsgBox>(area, "_confirmation");
                if (confirmation != null)
                    Click(confirmation, "_confirmButton");
                return;
            }
            NetworkClient.Send(new PlayerOperationRequest
            {
                RoomId = _roomId,
                PlayerIndex = info.CurrentPlayerIndex,
                HandId = info.HandId,
                TurnId = info.TurnId,
                Type = operation,
                Number = amount,
            });
        }

        private bool AcknowledgeTransition(bool spectator)
        {
            var box = FindObjectsOfType<UI.MsgBox>().FirstOrDefault(item => item.Title == "参战人数不足");
            if (box == null || !box.Message.Contains(spectator ? "房间列表" : "房间等待"))
            {
                Fail("Automatic table exit did not explain the role-specific destination.");
                return false;
            }
            Click(box, "_confirmButton");
            WriteResult($"client-{_clientIndex}.transition.checked", spectator ? "Lobby reason acknowledged" : "Waiting-room reason acknowledged");
            return true;
        }

        private void CheckOperationConfirmations(ClientGameInfo original)
        {
            _confirmationChecked = true;
            var area = Field<UI.GamePanelElement.OperationArea>(UI.GamePanel.Instance, "_operationArea");
            foreach (var button in new[] { "_allInButton", "_callButton", "_betOrRaiseButton", "_foldButton" })
            {
                var sample = original;
                sample.Players = original.Players.ToList();
                var player = sample.Players[sample.CurrentPlayerIndex];
                if (button == "_callButton")
                    sample.CurrentHighestPlayerBet = player.StageBet + player.Chips;
                if (button == "_foldButton")
                    sample.CurrentHighestPlayerBet = player.StageBet;
                area.Info = sample;
                if (button == "_betOrRaiseButton")
                    Field<TMPro.TMP_InputField>(area, "_betInput").text = (player.StageBet + player.Chips).ToString();
                Click(area, button);
                var box = Field<UI.MsgBox>(area, "_confirmation");
                if (box == null || !box.PreferCancel || Field<bool>(area, "_operationPending"))
                {
                    Fail("Risk action bypassed confirmation: " + button);
                    return;
                }
                var staleCallback = Field<Action>(box, "OnConfirm");
                Click(box, "_cancelButton");
                if (Field<bool>(area, "_operationPending"))
                {
                    Fail("Cancelling a risk action submitted a request.");
                    return;
                }
                area.Info = original;
                // A delayed confirmation must not submit against a later turn, even if it is ours again.
                var later = original;
                later.TurnId++;
                area.Info = later;
                staleCallback();
                if (Field<bool>(area, "_operationPending"))
                {
                    Fail("An expired confirmation submitted an action.");
                    return;
                }
                area.Info = original;
            }
            Click(area, "_allInButton");
            var open = Field<UI.MsgBox>(area, "_confirmation");
            var next = original;
            next.TurnId++;
            area.Info = next;
            if (Field<UI.MsgBox>(area, "_confirmation") != null || open.gameObject.activeSelf)
            {
                Fail("Turn advancement did not dismiss the open confirmation.");
                return;
            }
            area.Info = original;
            WriteResult("confirmation-lifecycle.checked", "All-in, all-in call/bet, free fold: cancel and stale callback send nothing; turn advance dismisses popup");
        }

        private void OnOperationResponse(PlayerOperationResponse response)
        {
            _actionPending = false;
            if (response.SuccessCode != 0)
                Fail("Operation failed with " + response.SuccessCode + ".");
        }

        private void OnRoundResult(RoundResultNotification message)
        {
            _roundsCompleted++;
            _actionPending = false;
            if (_testStorage)
            {
                if (_roundsCompleted != 1) Fail("A failed hand incorrectly broadcast a completed result.");
                if (_clientIndex == 0) WriteResult("storage-first-hand.finished", "finished");
                return;
            }
            if (_testSpectators)
            {
                if (_latestGameInfo.HasValue && _latestGameInfo.Value.Info.IsSpectator &&
                    message.RoundResult.PlayerResults.Any(player => player.Hole.Count != 0))
                    Fail("Spectator received another player's showdown hole cards.");
                return;
            }
            if (_testDepartures && _clientIndex == 2 && _roundsCompleted == 1 && _waitingLeavePhase == 1)
            {
                _waitingLeavePhase = 2;
                SendLeave(false);
            }
            if (_roundsCompleted >= 2 && !_finishing)
                StartCoroutine(FinishClient());
        }

        private void OnCountdown(InterHandCountdownSync message)
        {
            if (message.IsActive && message.SecondsRemaining <= 10)
                _sawCountdown = true;
        }

        private IEnumerator FinishClient()
        {
            _finishing = true;
            if (!_authenticated || !_joined || !_sawCountdown || !_sawRiver || !_sawBet || _roundsCompleted < 2)
            {
                Fail("Client did not observe the complete authentication, room, countdown and hand flow.");
                yield break;
            }
            if (_clientIndex == 2 && (!_sawFirstHandWaiting || !_sawSecondHandPlaying))
            {
                Fail("Mid-hand joiner did not transition from waiting to playing on the next hand.");
                yield break;
            }

            if (!(_testDepartures && _clientIndex == 0) && !_leaveCompleted)
                SendLeave(false);
            var departureDeadline = Time.realtimeSinceStartup + 5f;
            while (!_leaveCompleted && Time.realtimeSinceStartup < departureDeadline)
                yield return null;
            if (!_leaveCompleted || (_testDepartures && _clientIndex == 0 &&
                (!_leaveQueued || !_leaveCancelled || !_recoveredPending || !_sawDisconnectedTitle)) ||
                (_testDepartures && _clientIndex == 2 && (!_waitingLeaveCompleted || !_betweenHandLeaveCompleted)))
            {
                Fail("The complete departure and recovery flow was not observed.");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.2f);
            LoginCredentialStore.Clear();
            _networkRoom.StopClient();
            WriteResult(ResultPath(_clientIndex, "pass"), JsonSerializer.Serialize(new
            {
                Client = _clientIndex,
                UserName = _userName,
                Rounds = _roundsCompleted,
                SawCountdown = _sawCountdown,
                SawRiver = _sawRiver,
                SawBet = _sawBet,
                SawFirstHandWaiting = _sawFirstHandWaiting,
                SawSecondHandPlaying = _sawSecondHandPlaying,
                LeaveCompleted = _leaveCompleted,
                QueueAndCancel = _leaveQueued && _leaveCancelled,
                PendingRecovered = _recoveredPending,
                DisconnectedToTitle = _sawDisconnectedTitle,
                WaitingPlayerLeft = _waitingLeaveCompleted,
                BetweenHandsLeft = _betweenHandLeaveCompleted,
            }));
            Application.Quit(0);
        }

        private void SendJoin(int roomId)
        {
            var panel = UI.RoomListPanel.Instance;
            if (panel == null || !panel.gameObject.activeInHierarchy)
                return;
            var items = Field<Dictionary<int, UI.RoomListPanelElement.RoomListItem>>(panel, "_roomItems");
            if (!items.TryGetValue(roomId, out var item))
            {
                if (Time.realtimeSinceStartup >= _nextLobbyRefresh)
                {
                    _nextLobbyRefresh = Time.realtimeSinceStartup + 1f;
                    panel.RefreshRoomList();
                }
                return;
            }
            _roomId = roomId;
            _joined = true;
            Click(item, "_join");
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).GetValue(target);

        private void Click(UnityEngine.Component target, string name)
        {
            var button = target == null ? null : Field<UnityEngine.UI.Button>(target, name);
            var result = UI.GamePanelElement.RoundResultMsgBox.Instance;
            if (button != null && !button.IsInteractable() && result != null && result.IsShowing && target != result)
                Click(result, "_confirmButton");
            if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable())
            {
                Fail("UI button is unavailable: " + name);
                return;
            }
            button.onClick.Invoke();
        }

        private void InspectSpectatorState(GameInfoSync message)
        {
            var info = message.Info;
            if (info.IsSpectator)
            {
                _observerSnapshots++;
                var area = Field<UI.GamePanelElement.OperationArea>(UI.GamePanel.Instance, "_operationArea");
                if (info.Hole.Count != 0 || info.CanRaise ||
                    Field<List<UI.PokerElement>>(area, "_hole").Any(card => card.IsShowing) ||
                    area.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(button => button.gameObject.activeInHierarchy))
                {
                    Fail("Observer UI exposed private cards or betting controls.");
                    return;
                }
                if (info.RoomInfo.FindSeat(_userName) != null)
                    Fail("Observer occupies a seat.");
                if (_clientIndex == 0 && _roundsCompleted >= 2)
                    _sawSitOut = true;
            }

            if (_clientIndex == 2 && _roundsCompleted == 0)
            {
                if (_spectatorPhase == 0)
                {
                    if (!info.IsSpectator || message.CanJoinNextHand || info.RoomInfo.PlayerCount != 2 ||
                        Field<UnityEngine.UI.Button>(UI.GamePanel.Instance, "_participationButton").interactable)
                    {
                        Fail("Full table did not admit C solely as a spectator with disabled entry.");
                        return;
                    }
                    _spectatorPhase = 1;
                    Click(UI.GamePanel.Instance, "_keepSpectatingButton");
                }
                else if (_spectatorPhase == 1 && message.ParticipationRevision > 0)
                {
                    _spectatorPhase = 2;
                    NetworkClient.Send(new TableParticipationRequest
                    {
                        RoomId = _roomId, HandId = info.HandId, Revision = message.ParticipationRevision,
                        Action = TableParticipationAction.JoinNextHand,
                    });
                }
                else if (_spectatorPhase == 2 && _spectatorFullDenied)
                {
                    _spectatorPhase = 3;
                    Click(UI.GamePanel.Instance, "_leaveButton");
                }
                else if (_spectatorPhase == 4)
                {
                    _spectatorPhase = 5;
                    File.WriteAllText(Path.Combine(_outputDirectory, "observer-preflight"), "ready");
                }
            }
            if (_roundsCompleted == 1 && info.Round < GameStage.RoundOver)
            {
                if (_clientIndex == 0 && !_sitOutRequested)
                {
                    _sitOutRequested = true;
                    Click(UI.GamePanel.Instance, "_participationButton");
                }
                if (_clientIndex == 2 && message.CanJoinNextHand && !message.JoinNextHand &&
                    Field<UnityEngine.UI.Button>(UI.GamePanel.Instance, "_participationButton").interactable)
                    Click(UI.GamePanel.Instance, "_participationButton");
                if (_clientIndex == 2 && message.JoinNextHand)
                    File.WriteAllText(Path.Combine(_outputDirectory, "next-hand-reserved"), "ready");
            }
            if (_roundsCompleted == 2 && info.Round < GameStage.RoundOver && _clientIndex == 2)
            {
                if (info.IsSpectator || info.Hole.Count != 2)
                {
                    Fail("Applicant was not dealt into the following hand.");
                    return;
                }
                if (!_sitOutRequested)
                {
                    _sitOutRequested = true;
                    Click(UI.GamePanel.Instance, "_participationButton");
                }
                if (message.SitOutAfterHand)
                    File.WriteAllText(Path.Combine(_outputDirectory, "third-hand-sitout"), "ready");
            }
        }

        private IEnumerator FinishSpectatorClient()
        {
            _finishing = true;
            if (_roundsCompleted != 3 || !_sawRiver || !_sawBet || !_sawCountdown ||
                (_clientIndex == 0 && !_sawSitOut) || (_clientIndex == 1 && !_sawWaitingRoom) ||
                (_clientIndex == 2 && (!_spectatorFullDenied || !_waitingLeaveCompleted || !_sawSecondHandPlaying)))
            {
                Fail("Spectator lifecycle did not complete every required checkpoint.");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.2f);
            LoginCredentialStore.Clear();
            _networkRoom.StopClient();
            WriteResult(ResultPath(_clientIndex, "pass"), JsonSerializer.Serialize(new
            {
                Client = _clientIndex, Rounds = _roundsCompleted, ObserverSnapshots = _observerSnapshots,
                FullTableRejected = _spectatorFullDenied, ObserverLeftAndRejoined = _waitingLeaveCompleted,
                BecameSpectator = _sawSitOut, ReturnedToWaitingRoom = _sawWaitingRoom,
                ApplicantPlayedNextHand = _sawSecondHandPlaying, LeftToLobby = _leaveCompleted,
                ShippingUIButtons = true,
            }));
            Application.Quit(0);
        }

        private bool TryReadRoomId(out int roomId)
        {
            roomId = -1;
            var path = Path.Combine(_outputDirectory, "room.id");
            return File.Exists(path) && int.TryParse(File.ReadAllText(path), out roomId);
        }

        private string ResultPath(int clientIndex, string extension)
        {
            return Path.Combine(_outputDirectory, $"client-{clientIndex}.{extension}");
        }

        private void WriteResult(string path, string contents)
        {
            if (!Path.IsPathRooted(path))
                path = Path.Combine(_outputDirectory, path);
            File.WriteAllText(path, contents);
        }

        private void Fail(string reason)
        {
            if (_finishing && _role == "client" && File.Exists(ResultPath(_clientIndex, "pass")))
                return;

            _finishing = true;
            if (_role == "client")
                LoginCredentialStore.Clear();
            var fileName = _role == "server" ? "server.fail" : $"client-{_clientIndex}.fail";
            WriteResult(fileName, reason);
            Debug.LogError("Poker integration failure: " + reason);
            if (_networkRoom != null)
            {
                if (NetworkClient.active)
                    _networkRoom.StopClient();
                if (NetworkServer.active)
                    _networkRoom.StopServer();
            }
            Application.Quit(1);
        }

        private static string GetArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (arguments[index] == name)
                    return arguments[index + 1];
            }
            return null;
        }

        private static string SanitizeFileName(string value)
        {
            var invalidCharacters = Path.GetInvalidFileNameChars();
            return new string(value.Where(character => !invalidCharacters.Contains(character)).ToArray());
        }
    }
}
