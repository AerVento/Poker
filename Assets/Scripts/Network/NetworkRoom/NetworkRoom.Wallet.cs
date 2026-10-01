using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;
using Mirror;
using UnityEngine;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        private readonly Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)> _walletRequests =
            new Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)>();
        private ulong _nextWalletRequest;
        public event Action<WalletResponse> WalletReceived;
        public bool CanOpenWallet => HasAuthenticatedClient && !_roomEntryPending;

        private IWalletRepository WalletRepository =>
            (authenticator as AccountAuthenticator)?.AccountRepository as IWalletRepository;

        private void UpdateWalletHandAccess(PlayerAccount account)
        {
            if (account == null || !_roomManager.WhichRoom(account.UserName, out var roomId)) return;
            var game = _gameManager.GetGameInfo(roomId);
            if (game == null || !_gameManager.IsHandInProgress(roomId)) return;
            var player = game.Players.FirstOrDefault(p => p.IsOccupied && p.AccountId == account.AccountId && p.ParticipatedThisHand);
            if (player != null)
                WalletRepository?.SetHandWalletAccess(game.HandId, account.AccountId, player.RoundBet, player.IsFold);
        }

        private WalletSnapshot ReadCurrentWallet(PlayerAccount account)
        {
            UpdateWalletHandAccess(account);
            return WalletRepository?.ReadWallet(account.AccountId);
        }

        private void SynchronizeWalletBalance(PlayerAccount account)
        {
            if (!_roomManager.WhichRoom(account.UserName, out var roomId)) return;
            _gameManager.RefreshWalletBalance(roomId, account);
            BroadcastGameInfo(roomId);
            BroadcastRoundReady(roomId);
        }

        private void RegisterWalletServerHandlers()
        {
            _walletRequests.Clear();
            RegisterServerHandler<WalletRequest>((conn, request) =>
            {
                var response = ProcessWalletRequest(conn, request);
                if (response.HasSnapshot)
                    conn.Send(new AccountBalanceSync { Chips = response.Chips, Dollars = response.Dollars });
                conn.Send(response);
            }, requireAuthentication: true);
        }

        internal WalletResponse ProcessWalletRequest(NetworkConnectionToClient conn, WalletRequest request)
        {
            var response = new WalletResponse { RequestId = request.RequestId, IsExchange = request.IsExchange,
                OperationId = request.OperationId, Result = WalletResult.Unavailable };
            if (_serverStorageFailed || conn == null || !conn.isAuthenticated ||
                !_playerManager.TryGetSession(conn, out var session) || WalletRepository == null) return response;
            response.AccountId = session.Account.AccountId;
            _walletRequests.TryGetValue(conn, out var prior);
            if (request.RequestId == 0 || request.RequestId <= prior.Sequence ||
                (request.OperationId?.Length ?? 0) > 32 ||
                request.IsExchange && (!Guid.TryParseExact(request.OperationId, "N", out _) || request.Amount == 0))
            {
                response.Result = WalletResult.InvalidRequest;
                return response;
            }
            var now = Time.realtimeSinceStartupAsDouble;
            _walletRequests[conn] = (request.RequestId, prior.Next);
            if (now < prior.Next)
            {
                response.Result = WalletResult.RateLimited;
                return response;
            }
            _walletRequests[conn] = (request.RequestId, now + 0.15d);
            UpdateWalletHandAccess(session.Account);
            response.Result = request.IsExchange ? WalletRepository.ExchangeWallet(response.AccountId,
                request.OperationId, request.DollarsToChips, request.Amount) : WalletResult.Success;
            var snapshot = WalletRepository.ReadWallet(response.AccountId);
            if (snapshot == null) return response;
            session.Account = snapshot.Account.Copy();
            response.HasSnapshot = true;
            response.Dollars = snapshot.Account.Dollars;
            response.Chips = snapshot.Account.Chips;
            response.AvailableChips = snapshot.AvailableChips;
            response.CanExchange = snapshot.CanExchange;
            if (request.IsExchange && (response.Result == WalletResult.Success || response.Result == WalletResult.AlreadyApplied))
                SynchronizeWalletBalance(snapshot.Account);
            return response;
        }

        public ulong RequestWallet()
        {
            if (!CanOpenWallet) return 0;
            var request = ++_nextWalletRequest;
            NetworkClient.Send(new WalletRequest { RequestId = request });
            return request;
        }

        public ulong ExchangeWallet(bool dollarsToChips, uint amount, string operationId = null)
        {
            if (!CanOpenWallet || amount == 0) return 0;
            var request = ++_nextWalletRequest;
            NetworkClient.Send(new WalletRequest { RequestId = request, IsExchange = true,
                DollarsToChips = dollarsToChips, Amount = amount, OperationId = operationId ?? Guid.NewGuid().ToString("N") });
            return request;
        }

        private void RegisterWalletClientHandlers()
        {
            NetworkClient.RegisterHandler<WalletResponse>(response =>
            {
                if (CanOpenWallet && response.AccountId == GameController.Instance.State.AccountId)
                    WalletReceived?.Invoke(response);
            });
        }
    }
}
