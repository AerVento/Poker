using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;
using Game.UI;
using Mirror;
using UnityEngine;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        private NamecardServerCatalog _shopCatalog;
        private readonly Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)> _shopRequests =
            new Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)>();
        private ulong _nextShopRequest;

        public bool CanOpenShop => HasAuthenticatedClient && !_roomEntryPending;

        private void RegisterShopServerHandlers()
        {
            _shopRequests.Clear();
            _shopCatalog = NamecardCatalogLoader.LoadServer();
            RegisterServerHandler<NamecardShopRequest>((conn, request) =>
            {
                var response = ProcessShopRequest(conn, request);
                if (response.HasSnapshot)
                    conn.Send(new AccountBalanceSync { Chips = response.Chips, Dollars = response.Dollars });
                conn.Send(response);
                if (request.Action != ShopAction.Read && response.HasSnapshot)
                    conn.Send(ReadCollection(conn));
            }, requireAuthentication: true);
        }

        internal NamecardShopResponse ProcessShopRequest(NetworkConnectionToClient conn, NamecardShopRequest request)
        {
            var response = new NamecardShopResponse { RequestId = request.RequestId, Result = ShopResult.Unavailable };
            if (_serverStorageFailed || conn == null || !conn.isAuthenticated ||
                !_playerManager.TryGetSession(conn, out var session))
                return response;
            response.AccountId = session.Account.AccountId;
            _shopRequests.TryGetValue(conn, out var previous);
            if (request.RequestId == 0 || request.RequestId <= previous.Sequence ||
                !Enum.IsDefined(typeof(ShopAction), request.Action) ||
                (request.CatalogRevision?.Length ?? 0) > 64 ||
                (request.OperationId?.Length ?? 0) > 32 || (request.ProductId?.Length ?? 0) > 48)
            {
                response.Result = ShopResult.InvalidRequest;
                return response;
            }
            var now = Time.realtimeSinceStartupAsDouble;
            _shopRequests[conn] = (request.RequestId, previous.Next);
            if (now < previous.Next)
            {
                response.Result = ShopResult.RateLimited;
                return response;
            }
            _shopRequests[conn] = (request.RequestId, now + 0.2d);
            if (request.Action == ShopAction.Equip && (_roomManager.WhichRoom(session.Account.UserName, out _) || session.Account.LastRoomId.HasValue))
            {
                response.Result = ShopResult.OutsideRoomRequired;
                return response;
            }
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository as INamecardRepository;
            if (repository == null || _shopCatalog == null)
                return response;
            UpdateWalletHandAccess(session.Account);
            response.Result = request.Action == ShopAction.Read ? ShopResult.Success :
                repository is IWalletPaymentRepository payments
                    ? payments.ChangeNamecard(response.AccountId, request.OperationId, request.Action,
                        request.ProductId, request.CatalogRevision, _shopCatalog, request.AuthorizeChipConversion, request.AuthorizedShortfall)
                    : repository.ChangeNamecard(response.AccountId, request.OperationId, request.Action,
                        request.ProductId, request.CatalogRevision, _shopCatalog);
            if (response.Result == ShopResult.ConversionRequired)
            {
                var current = WalletRepository?.ReadWallet(response.AccountId)?.Account;
                var product = _shopCatalog.Products.FirstOrDefault(p => p.Id == request.ProductId);
                if (current != null && product != null && product.Price > current.Dollars)
                    response.RequiredConversion = product.Price - current.Dollars;
            }
            if (response.Result == ShopResult.Success && request.Action == ShopAction.Purchase)
            {
                var current = WalletRepository?.ReadWallet(response.AccountId)?.Account;
                if (current != null) SynchronizeWalletBalance(current);
            }
            return PopulateNamecardSnapshot(response, repository, session);
        }

        private NamecardShopResponse PopulateNamecardSnapshot(NamecardShopResponse response, INamecardRepository repository, PlayerSession session)
        {
            var snapshot = repository.ReadNamecards(response.AccountId);
            if (snapshot == null)
                return response;
            session.Account = snapshot.Account.Copy();
            response.HasSnapshot = true;
            response.Chips = snapshot.Account.Chips;
            response.Dollars = snapshot.Account.Dollars;
            var wallet = ReadCurrentWallet(snapshot.Account);
            response.AvailableChips = wallet?.AvailableChips ?? 0;
            response.CanExchange = wallet?.CanExchange ?? false;
            response.OwnedIds = snapshot.OwnedIds;
            response.EquippedId = snapshot.Account.EquippedNamecardId;
            response.CanAct = true;
            response.CanEquip = snapshot.CanAct;
            response.CatalogRevision = _shopCatalog.Revision;
            response.Offers = _shopCatalog.Products.Select(p =>
                new NamecardOfferMessage { Id = p.Id, Price = p.Price, Listed = p.Listed }).ToArray();
            return response;
        }

        public ulong RequestShop(ShopAction action = ShopAction.Read, string productId = null,
            string revision = null, string operationId = null, bool authorizeChipConversion = false, uint authorizedShortfall = 0)
        {
            if (!CanOpenShop)
                return 0;
            if (action != ShopAction.Read) InvalidateCollection();
            var requestId = ++_nextShopRequest;
            NetworkClient.Send(new NamecardShopRequest
            {
                RequestId = requestId, Action = action, ProductId = productId,
                CatalogRevision = revision, OperationId = operationId,
                AuthorizeChipConversion = authorizeChipConversion, AuthorizedShortfall = authorizedShortfall
            });
            return requestId;
        }

        private void RegisterShopClientHandlers()
        {
            NetworkClient.RegisterHandler<NamecardShopResponse>(response =>
            {
                if (CanOpenShop && response.AccountId == GameController.Instance.State.AccountId)
                    ShopPanel.Instance?.Receive(response);
            });
        }

        private RoomInfo PopulateNamecards(RoomInfo room)
        {
            if (room == null)
                return null;
            if (room.Namecards == null)
                room.Namecards = new Dictionary<string, string>();
            if (room.ServiceYears == null)
                room.ServiceYears = new Dictionary<string, ulong>();
            var visibleNames = new HashSet<string>(room.MemberNames);
            var game = _gameManager.GetGameInfo(room.Id);
            if (game != null)
                foreach (var player in game.Players)
                    if (player.IsOccupied) visibleNames.Add(player.PlayerName);
            foreach (var stale in room.Namecards.Keys.Where(name => !visibleNames.Contains(name)).ToArray())
                room.Namecards.Remove(stale);
            foreach (var stale in room.ServiceYears.Keys.Where(name => !visibleNames.Contains(name)).ToArray())
                room.ServiceYears.Remove(stale);
            // Keep disconnected / departed hand participants' last public appearance until their hand ends.
            var accounts = new Dictionary<string, PlayerAccount>();
            foreach (var name in room.MemberNames)
            {
                if (!_playerManager.TryGetAccount(name, out var account)) continue;
                accounts[name] = account;
                room.Namecards[name] = account.EquippedNamecardId ?? string.Empty;
            }
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository as IServiceYearsRepository;
            var years = repository?.ReadServiceYears(accounts.Values.Select(account => account.AccountId).ToArray());
            foreach (var entry in accounts)
                room.ServiceYears[entry.Key] = years != null && years.TryGetValue(entry.Value.AccountId, out var total)
                    ? total : 0UL;
            return room;
        }
    }
}
