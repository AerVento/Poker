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
        private bool _pendingRequestSent;

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
                AccountRepository = new SqliteAccountRepository(databasePath);
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
            }
#else
            _accountService = null;
            AccountRepository = null;
            Log.WriteError("The account database server is only supported in the Unity Editor and standalone builds.");
#endif

            NetworkServer.RegisterHandler<AccountRegisterRequest>(OnRegisterRequest, requireAuthentication: false);
            NetworkServer.RegisterHandler<AccountLoginRequest>(OnLoginRequest, requireAuthentication: false);
        }

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<AccountRegisterRequest>();
            NetworkServer.UnregisterHandler<AccountLoginRequest>();
            _accountService = null;
            AccountRepository = null;
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            // Wait for an explicit register or login request from the client.
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<AccountAuthenticationResponse>(
                OnAuthenticationResponse,
                requireAuthentication: false);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<AccountAuthenticationResponse>();
            ClearPendingAuthentication();
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

        public void CancelPendingAuthentication()
        {
            ClearPendingAuthentication();
        }

        private void BeginAuthentication(
            AccountAuthenticationOperation operation,
            string userName,
            string password)
        {
            if (NetworkClient.connection != null && NetworkClient.connection.isAuthenticated)
                return;

            _pendingOperation = operation;
            _pendingUserName = userName;
            _pendingPassword = password;
            _pendingRequestSent = false;

            if (NetworkClient.isConnected)
            {
                SendPendingAuthentication();
            }
            else if (!NetworkClient.active)
            {
                _networkRoom.StartClient();
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
            else
            {
                NetworkClient.Send(new AccountLoginRequest
                {
                    UserName = _pendingUserName,
                    Password = _pendingPassword,
                });
            }
        }

        private void OnRegisterRequest(NetworkConnectionToClient conn, AccountRegisterRequest request)
        {
            Authenticate(
                conn,
                AccountAuthenticationOperation.Register,
                () => _accountService?.Register(request.UserName, request.Password));
        }

        private void OnLoginRequest(NetworkConnectionToClient conn, AccountLoginRequest request)
        {
            Authenticate(
                conn,
                AccountAuthenticationOperation.Login,
                () => _accountService?.Login(request.UserName, request.Password));
        }

        private void Authenticate(
            NetworkConnectionToClient conn,
            AccountAuthenticationOperation operation,
            Func<AccountAuthenticationResult> authenticate)
        {
            if (conn == null || conn.isAuthenticated)
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

            if (!result.IsSuccess)
            {
                conn.Send(CreateResponse(operation, result, false));
                return;
            }

            _networkRoom.ValidateLastRoom(result.Account);
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

            ClearPendingAuthentication();
            if (response.Code != AccountAuthenticationCode.Success)
            {
                AuthenticationResponseReceived?.Invoke(response);
                return;
            }

            if (GameController.Available)
            {
                GameController.Instance.State.SetAuthenticatedAccount(
                    response.AccountId,
                    response.UserName,
                    response.Chips,
                    response.HasLastRoom ? (int?)response.LastRoomId : null);
            }

            AuthenticationResponseReceived?.Invoke(response);
            ClientAccept();
            _networkRoom.HandleClientAuthentication(response);
        }

        private void ClearPendingAuthentication()
        {
            _pendingOperation = null;
            _pendingUserName = null;
            _pendingPassword = null;
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
            return response;
        }
    }
}
