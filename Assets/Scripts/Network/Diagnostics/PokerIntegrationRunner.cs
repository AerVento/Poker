using Assets.Scripts.Network.Messages;
using Game.Network.Accounts;
using Mirror;
using System;
using System.Collections;
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

        public static bool TryStart(GameController controller)
        {
            var role = GetArgument("-pokerIntegrationRole");
            if (role != "server" && role != "client")
                return false;

            var runId = GetArgument("-pokerIntegrationRun");
            var outputDirectory = GetArgument("-pokerIntegrationOutput");
            if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(outputDirectory))
                throw new InvalidOperationException("Integration run and output arguments are required.");

            var runner = controller.gameObject.AddComponent<PokerIntegrationRunner>();
            runner._role = role;
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

            if (_role == "server")
                StartCoroutine(RunServer());
            else
                StartCoroutine(RunClient());
        }

        private IEnumerator RunServer()
        {
            _authenticator.ConfigureDatabaseFileName($"PokerIntegration-{_runId}.db");
            _networkRoom.StartServer();
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

                if (Enumerable.Range(0, 3).All(index => File.Exists(ResultPath(index, "pass"))))
                {
#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
                    var repository = _authenticator.AccountRepository as SqliteAccountRepository;
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

        private IEnumerator RunClient()
        {
            if (_clientIndex < 0 || _clientIndex > 2)
            {
                Fail("Client index must be between 0 and 2.");
                yield break;
            }

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
                    if (_clientIndex == 0 && _roomId < 0)
                    {
                        NetworkClient.Send(new PlayerCreateRoomRequest { PlayerName = _userName });
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

                yield return new WaitForSecondsRealtime(0.05f);
            }

            if (!_finishing)
                Fail("Timed out before completing the scripted client flow.");
        }

        private void RegisterClientHandlers()
        {
            NetworkClient.RegisterHandler<RoomInfoSync>(OnRoomInfoSync, false);
            NetworkClient.RegisterHandler<PlayerJoinRoomResponse>(OnJoinRoomResponse, false);
            NetworkClient.RegisterHandler<PlayerStartGameResponse>(OnStartGameResponse, false);
            NetworkClient.RegisterHandler<GameInfoSync>(OnGameInfoSync, false);
            NetworkClient.RegisterHandler<PlayerOperationResponse>(OnOperationResponse, false);
            NetworkClient.RegisterHandler<RoundResultNotification>(OnRoundResult, false);
            NetworkClient.RegisterHandler<InterHandCountdownSync>(OnCountdown, false);
            NetworkClient.RegisterHandler<AccountBalanceSync>(_ => { }, false);
        }

        private void OnAuthenticationResponse(AccountAuthenticationResponse response)
        {
            if (response.Code != AccountAuthenticationCode.Success)
            {
                Fail("Authentication failed with " + response.Code + ".");
                return;
            }

            _authenticated = true;
        }

        private void OnRoomInfoSync(RoomInfoSync message)
        {
            var room = message.RoomInfo;
            if (room == null)
                return;

            _roomId = room.Id;
            _joined = room.ContainsMember(_userName);
            if (_clientIndex == 0)
            {
                File.WriteAllText(Path.Combine(_outputDirectory, "room.id"), room.Id.ToString());
                var otherPlayersReady = room.Seats
                    .Where(seat => seat.PlayerName != _userName)
                    .All(seat => seat.IsReady);
                if (!_startSent && room.PlayerCount >= 2 && otherPlayersReady)
                {
                    _startSent = true;
                    NetworkClient.Send(new PlayerStartGameRequest
                    {
                        PlayerName = _userName,
                        RoomId = room.Id,
                    });
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
                NetworkClient.Send(new PlayerChangeReadyMessage
                {
                    PlayerName = _userName,
                    RoomId = _roomId,
                });
            }
            if (_clientIndex == 2)
                File.WriteAllText(Path.Combine(_outputDirectory, "client-2.joined"), "joined");
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

            _roomId = info.RoomInfo.Id;
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

            _actionPending = true;
            NetworkClient.Send(new PlayerOperationRequest
            {
                RoomId = _roomId,
                PlayerIndex = info.CurrentPlayerIndex,
                Type = PlayerOperationType.Fold,
            });
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
            if (!_authenticated || !_joined || !_sawCountdown || _roundsCompleted < 2)
            {
                Fail("Client did not observe the complete authentication, room, countdown and hand flow.");
                yield break;
            }
            if (_clientIndex == 2 && (!_sawFirstHandWaiting || !_sawSecondHandPlaying))
            {
                Fail("Mid-hand joiner did not transition from waiting to playing on the next hand.");
                yield break;
            }

            NetworkClient.Send(new PlayerLeaveRoomRequest
            {
                PlayerName = _userName,
                RoomId = _roomId,
            });
            yield return new WaitForSecondsRealtime(0.5f);
            _networkRoom.StopClient();
            WriteResult(ResultPath(_clientIndex, "pass"), JsonSerializer.Serialize(new
            {
                Client = _clientIndex,
                UserName = _userName,
                Rounds = _roundsCompleted,
                SawCountdown = _sawCountdown,
                SawFirstHandWaiting = _sawFirstHandWaiting,
                SawSecondHandPlaying = _sawSecondHandPlaying,
            }));
            Application.Quit(0);
        }

        private void SendJoin(int roomId)
        {
            _roomId = roomId;
            _joined = true;
            NetworkClient.Send(new PlayerJoinRoomRequest
            {
                PlayerName = _userName,
                RoomId = roomId,
            });
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
