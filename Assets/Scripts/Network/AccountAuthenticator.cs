using Cysharp.Threading.Tasks;
using Framework.Log;
using Game.Network.Accounts;
using Mirror;
using System;
using System.IO;
using UnityEngine;

namespace Game.Network
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkRoom))]
    public sealed class AccountAuthenticator : NetworkAuthenticator
    {
        [SerializeField]
        private string _databaseFileName = "PokerAccounts.db";

        [SerializeField]
        private uint _initialChips = AccountService.DefaultInitialChips;

        private NetworkRoom _networkRoom;
        private AccountService _accountService;
        private AccountAuthenticationOperation? _pendingOperation;
        private string _pendingUserName;
        private string _pendingPassword;
        private string _pendingSessionToken;
        private bool _pendingRequestSent;
        private bool _logoutPending;
        private string _logoutSessionToken;

        public event Action<AccountAuthenticationResponse> AuthenticationResponseReceived;

        public bool IsAuthenticationPending => _pendingOperation.HasValue;

        public IHandAccountRepository AccountRepository { get; private set; }

        public void ConfigureDatabaseFileName(string databaseFileName)
        {
            if (NetworkServer.active)
                throw new InvalidOperationException("The account database cannot be changed after the server starts.");
            if (string.IsNullOrWhiteSpace(databaseFileName) || Path.GetFileName(databaseFileName) != databaseFileName)
                throw new ArgumentException("A database file name without a directory is required.", nameof(databaseFileName));

            _databaseFileName = databaseFileName;
        }

        private void Awake()
        {
            _networkRoom = GetComponent<NetworkRoom>();
        }

        public override void OnStartServer()
        {
#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
            try
            {
                var databasePath = Path.Combine(Application.persistentDataPath, _databaseFileName);
                AccountRepository = new GuardedAccountRepository(
                    new SqliteAccountRepository(databasePath), _networkRoom.HandleFatalStorageError);
                _accountService = new AccountService(
                    AccountRepository,
                    initialChips: _initialChips);
                _accountService.Initialize();
                Log.WriteInfo($"Account database initialized at {databasePath}.");
            }
            catch (Exception exception)
            {
                _accountService = null;
                AccountRepository = null;
                Log.WriteError($"Unable to initialize the account database: {exception}");
                _networkRoom.HandleFatalStorageError(exception);
            }
#else
            _accountService = null;
            AccountRepository = null;
            Log.WriteError("The account database server is only supported in the Unity Editor and standalone builds.");
#endif

            NetworkServer.RegisterHandler<AccountRegisterRequest>(OnRegisterRequest, requireAuthentication: false);
            NetworkServer.RegisterHandler<AccountLoginRequest>(OnLoginRequest, requireAuthentication: false);
            NetworkServer.RegisterHandler<AccountTokenLoginRequest>(OnTokenLoginRequest, requireAuthentication: false);
            NetworkServer.RegisterHandler<AccountLogoutRequest>(OnLogoutRequest, requireAuthentication: true);
        }

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<AccountRegisterRequest>();
            NetworkServer.UnregisterHandler<AccountLoginRequest>();
            NetworkServer.UnregisterHandler<AccountTokenLoginRequest>();
            NetworkServer.UnregisterHandler<AccountLogoutRequest>();
            _accountService = null;
            AccountRepository = null;
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            if (_networkRoom.IsServerShuttingDown)
            {
                conn.Send(new ServerShutdownNotice { Reason = ServerShutdownReason.StorageFailure });
                return;
            }
            _networkRoom.RecordClientActivity(conn);
            // Wait for an explicit register or login request from the client.
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<AccountAuthenticationResponse>(
                OnAuthenticationResponse,
                requireAuthentication: false);
            NetworkClient.RegisterHandler<AccountLogoutResponse>(OnLogoutResponse, requireAuthentication: false);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<AccountAuthenticationResponse>();
            NetworkClient.UnregisterHandler<AccountLogoutResponse>();
            ClearPendingAuthentication();
            _logoutPending = false;
            _logoutSessionToken = null;
        }

        public override void OnClientAuthenticate()
        {
            SendPendingAuthentication();
        }

        public void Register(string userName, string password)
        {
            BeginAuthentication(AccountAuthenticationOperation.Register, userName, password);
        }

        public void Login(string userName, string password)
        {
            BeginAuthentication(AccountAuthenticationOperation.Login, userName, password);
        }

        public bool LoginWithSavedCredentials(string sessionToken = null)
        {
            if (string.IsNullOrEmpty(sessionToken) && !LoginCredentialStore.TryLoad(out _, out sessionToken))
                return false;

            BeginAuthentication(
                AccountAuthenticationOperation.TokenLogin,
                null,
                null,
                sessionToken);
            return true;
        }

        public void Logout()
        {
            if (_logoutPending)
                return;

            var sessionToken = GameController.Available
                ? GameController.Instance.State.SessionToken
                : null;
            if (string.IsNullOrEmpty(sessionToken) &&
                LoginCredentialStore.TryLoad(out _, out var savedSessionToken))
            {
                sessionToken = savedSessionToken;
            }

            if (NetworkClient.connection == null ||
                !NetworkClient.connection.isAuthenticated ||
                string.IsNullOrEmpty(sessionToken))
            {
                CompleteLocalLogout();
                return;
            }

            _logoutPending = true;
            _logoutSessionToken = sessionToken;
            LoginCredentialStore.ClearIfMatches(sessionToken);
            if (GameController.Available)
                GameController.Instance.State.ClearAuthenticatedAccount();
            NetworkClient.Send(new AccountLogoutRequest { SessionToken = sessionToken });
            UniTask.Void(async () =>
            {
                await UniTask.Delay(2000);
                if (_logoutPending)
                    CompleteLocalLogout();
            });
        }

        public void CancelPendingAuthentication()
        {
            ClearPendingAuthentication();
        }

        private void BeginAuthentication(
            AccountAuthenticationOperation operation,
            string userName,
            string password,
            string sessionToken = null)
        {
            if (NetworkClient.connection != null && NetworkClient.connection.isAuthenticated)
                return;
            if (GameController.Available && GameController.Instance.HasNetworkInitializationStarted &&
                !GameController.Instance.IsClientNetworkReady)
                return;

            _pendingOperation = operation;
            _pendingUserName = userName;
            _pendingPassword = password;
            _pendingSessionToken = sessionToken;
            _pendingRequestSent = false;
            _networkRoom.PromoteSilentConnectionAttempt();

            if (NetworkClient.isConnected)
            {
                SendPendingAuthentication();
            }
            else if (!NetworkClient.active)
            {
                if (!_networkRoom.StartConfiguredClientConnection())
                    ClearPendingAuthentication();
            }
        }

        private void SendPendingAuthentication()
        {
            if (!_pendingOperation.HasValue ||
                _pendingRequestSent ||
                !NetworkClient.isConnected ||
                NetworkClient.connection == null ||
                NetworkClient.connection.isAuthenticated)
            {
                return;
            }

            _pendingRequestSent = true;
            if (_pendingOperation.Value == AccountAuthenticationOperation.Register)
            {
                NetworkClient.Send(new AccountRegisterRequest
                {
                    UserName = _pendingUserName,
                    Password = _pendingPassword,
                });
            }
            else if (_pendingOperation.Value == AccountAuthenticationOperation.Login)
            {
                NetworkClient.Send(new AccountLoginRequest
                {
                    UserName = _pendingUserName,
                    Password = _pendingPassword,
                });
            }
            else
            {
                NetworkClient.Send(new AccountTokenLoginRequest
                {
                    SessionToken = _pendingSessionToken,
                });
            }
        }

        private void OnRegisterRequest(NetworkConnectionToClient conn, AccountRegisterRequest request)
        {
            _networkRoom.RecordClientActivity(conn);
            Authenticate(
                conn,
                AccountAuthenticationOperation.Register,
                () => _accountService?.Register(request.UserName, request.Password));
        }

        private void OnLoginRequest(NetworkConnectionToClient conn, AccountLoginRequest request)
        {
            _networkRoom.RecordClientActivity(conn);
            Authenticate(
                conn,
                AccountAuthenticationOperation.Login,
                () => _accountService?.Login(request.UserName, request.Password));
        }

        private void OnTokenLoginRequest(NetworkConnectionToClient conn, AccountTokenLoginRequest request)
        {
            _networkRoom.RecordClientActivity(conn);
            Authenticate(
                conn,
                AccountAuthenticationOperation.TokenLogin,
                () => _accountService?.LoginWithToken(request.SessionToken));
        }

        private void OnLogoutRequest(NetworkConnectionToClient conn, AccountLogoutRequest request)
        {
            _networkRoom.RecordClientActivity(conn);
            if (_networkRoom.IsServerShuttingDown)
                return;
            var revoked = false;
            try
            {
                revoked = _accountService?.Logout(request.SessionToken) == true;
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to revoke account session: {exception}");
            }

            if (!_networkRoom.IsServerShuttingDown)
                conn.Send(new AccountLogoutResponse { Revoked = revoked });
        }

        private void Authenticate(
            NetworkConnectionToClient conn,
            AccountAuthenticationOperation operation,
            Func<AccountAuthenticationResult> authenticate)
        {
            if (_networkRoom.IsServerShuttingDown || conn == null || conn.isAuthenticated)
                return;

            AccountAuthenticationResult result;
            try
            {
                result = authenticate() ?? new AccountAuthenticationResult
                {
                    Code = AccountAuthenticationCode.StorageUnavailable,
                };
            }
            catch (Exception exception)
            {
                Log.WriteError($"Account authentication failed due to a server error: {exception}");
                result = new AccountAuthenticationResult
                {
                    Code = AccountAuthenticationCode.StorageUnavailable,
                };
            }

            if (_networkRoom.IsServerShuttingDown)
                return;

            if (!result.IsSuccess)
            {
                conn.Send(CreateResponse(operation, result, false));
                return;
            }

            _networkRoom.ValidateLastRoom(result.Account);
            if (_networkRoom.IsServerShuttingDown)
                return;
            var canRestoreReplacedSession = _networkRoom.CanRestoreReplacedSession(result.Account);
            var oldConnection = _networkRoom.BindAuthenticatedAccount(result.Account, conn);
            var response = CreateResponse(
                operation,
                result,
                oldConnection == null || !canRestoreReplacedSession);
            conn.Send(response);
            ServerAccept(conn);

            if (oldConnection != null && oldConnection != conn)
            {
                Log.WriteInfo($"Account {result.Account.UserName} logged in again; replacing the previous connection.");
                if (canRestoreReplacedSession)
                    _networkRoom.RestoreReplacedSession(result.Account.UserName, conn);
                oldConnection.Disconnect();
            }
        }

        private void OnAuthenticationResponse(AccountAuthenticationResponse response)
        {
            if (!_pendingOperation.HasValue || response.Operation != _pendingOperation.Value)
                return;

            var attemptedSessionToken = _pendingSessionToken;
            ClearPendingAuthentication();
            if (response.Code != AccountAuthenticationCode.Success)
            {
                if (response.Operation == AccountAuthenticationOperation.TokenLogin)
                {
                    LoginCredentialStore.ClearIfMatches(attemptedSessionToken);
                    if (GameController.Available)
                        GameController.Instance.State.ClearAuthenticatedAccount();
                }
                AuthenticationResponseReceived?.Invoke(response);
                return;
            }

            LoginCredentialStore.Save(response.UserName, response.SessionToken);

            if (GameController.Available)
            {
                GameController.Instance.State.SetAuthenticatedAccount(
                    response.AccountId,
                    response.UserName,
                    response.Chips,
                    response.HasLastRoom ? (int?)response.LastRoomId : null,
                    response.SessionToken);
            }

            AuthenticationResponseReceived?.Invoke(response);
            ClientAccept();
            _networkRoom.HandleClientAuthentication(response);
        }

        private void OnLogoutResponse(AccountLogoutResponse response)
        {
            CompleteLocalLogout();
        }

        private void CompleteLocalLogout()
        {
            _logoutPending = false;
            if (string.IsNullOrEmpty(_logoutSessionToken))
                LoginCredentialStore.Clear();
            else
                LoginCredentialStore.ClearIfMatches(_logoutSessionToken);
            _logoutSessionToken = null;
            if (GameController.Available)
                GameController.Instance.State.ClearAuthenticatedAccount();
            _networkRoom.HandleClientLogout();
        }

        private void ClearPendingAuthentication()
        {
            _pendingOperation = null;
            _pendingUserName = null;
            _pendingPassword = null;
            _pendingSessionToken = null;
            _pendingRequestSent = false;
        }

        private static AccountAuthenticationResponse CreateResponse(
            AccountAuthenticationOperation operation,
            AccountAuthenticationResult result,
            bool requireRoomRecovery)
        {
            var response = new AccountAuthenticationResponse
            {
                Operation = operation,
                Code = result.Code,
            };

            if (result.Account == null)
                return response;

            response.AccountId = result.Account.AccountId;
            response.UserName = result.Account.UserName;
            response.Chips = result.Account.Chips;
            response.HasLastRoom = result.Account.LastRoomId.HasValue;
            response.LastRoomId = result.Account.LastRoomId.GetValueOrDefault();
            response.RequiresRoomRecovery = requireRoomRecovery && response.HasLastRoom;
            response.SessionToken = result.SessionToken;
            return response;
        }
    }
}
