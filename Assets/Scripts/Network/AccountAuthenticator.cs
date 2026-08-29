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
        private long _initialChips = AccountService.DefaultInitialChips;

        private NetworkRoom _networkRoom;
        private AccountService _accountService;

        public event Action<AccountAuthenticationResponse> AuthenticationResponseReceived;

        private void Awake()
        {
            _networkRoom = GetComponent<NetworkRoom>();
        }

        public override void OnStartServer()
        {
#if UNITY_EDITOR || UNITY_STANDALONE_LINUX
            try
            {
                var databasePath = Path.Combine(Application.persistentDataPath, _databaseFileName);
                _accountService = new AccountService(
                    new SqliteAccountRepository(databasePath),
                    initialChips: _initialChips);
                _accountService.Initialize();
                Log.WriteInfo($"Account database initialized at {databasePath}.");
            }
            catch (Exception exception)
            {
                _accountService = null;
                Log.WriteError($"Unable to initialize the account database: {exception}");
            }
#else
            _accountService = null;
            Log.WriteError("The account database server is only supported in the Unity Editor and Linux Standalone server.");
#endif

            NetworkServer.RegisterHandler<AccountRegisterRequest>(OnRegisterRequest, requireAuthentication: false);
            NetworkServer.RegisterHandler<AccountLoginRequest>(OnLoginRequest, requireAuthentication: false);
        }

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<AccountRegisterRequest>();
            NetworkServer.UnregisterHandler<AccountLoginRequest>();
            _accountService = null;
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
        }

        public override void OnClientAuthenticate()
        {
            // The login UI submits credentials after the transport connection is established.
        }

        public void Register(string userName, string password)
        {
            if (NetworkClient.connection == null || NetworkClient.connection.isAuthenticated)
                return;

            NetworkClient.Send(new AccountRegisterRequest
            {
                UserName = userName,
                Password = password,
            });
        }

        public void Login(string userName, string password)
        {
            if (NetworkClient.connection == null || NetworkClient.connection.isAuthenticated)
                return;

            NetworkClient.Send(new AccountLoginRequest
            {
                UserName = userName,
                Password = password,
            });
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

            var response = CreateResponse(operation, result);
            if (!result.IsSuccess)
            {
                conn.Send(response);
                return;
            }

            var oldConnection = _networkRoom.BindAuthenticatedAccount(result.Account, conn);
            conn.Send(response);
            ServerAccept(conn);

            if (oldConnection != null && oldConnection != conn)
            {
                Log.WriteInfo($"Account {result.Account.UserName} logged in again; replacing the previous connection.");
                oldConnection.Disconnect();
            }
        }

        private void OnAuthenticationResponse(AccountAuthenticationResponse response)
        {
            AuthenticationResponseReceived?.Invoke(response);
            if (response.Code != AccountAuthenticationCode.Success)
                return;

            if (GameController.Available)
            {
                GameController.Instance.State.SetAuthenticatedAccount(
                    response.AccountId,
                    response.UserName,
                    response.Chips,
                    response.HasLastRoom ? (int?)response.LastRoomId : null);
            }

            ClientAccept();
        }

        private static AccountAuthenticationResponse CreateResponse(
            AccountAuthenticationOperation operation,
            AccountAuthenticationResult result)
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
            return response;
        }
    }
}
