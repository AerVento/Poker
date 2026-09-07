using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

        public bool CanOpenShop => HasAuthenticatedClient && !_roomEntryPending &&
            !_activeClientRoomId.HasValue && !GameController.Instance.State.LastRoomId.HasValue;

        private void RegisterShopServerHandlers()
        {
            _shopRequests.Clear();
            var asset = Resources.Load<TextAsset>("SO/Shop/NamecardServerCatalog");
            if (asset == null)
                throw new InvalidOperationException("Missing exported namecard server catalog.");
            _shopCatalog = new NamecardServerCatalog
            {
                Products = JsonSerializer.Deserialize<NamecardOffer[]>(asset.text)
            };
            _shopCatalog.Validate();
            RegisterServerHandler<NamecardShopRequest>((conn, request) =>
            {
                var response = ProcessShopRequest(conn, request);
                if (response.HasSnapshot)
                    conn.Send(new AccountBalanceSync { Chips = response.Chips });
                conn.Send(response);
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
            if (_roomManager.WhichRoom(session.Account.UserName, out _) || session.Account.LastRoomId.HasValue)
            {
                response.Result = ShopResult.OutsideRoomRequired;
                return response;
            }
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository as INamecardRepository;
            if (repository == null || _shopCatalog == null)
                return response;
            response.Result = request.Action == ShopAction.Read ? ShopResult.Success :
                repository.ChangeNamecard(response.AccountId, request.OperationId, request.Action,
                    request.ProductId, request.CatalogRevision, _shopCatalog);
            var snapshot = repository.ReadNamecards(response.AccountId);
            if (snapshot == null)
                return response;
            session.Account = snapshot.Account.Copy();
            response.HasSnapshot = true;
            response.Chips = snapshot.Account.Chips;
            response.OwnedIds = snapshot.OwnedIds;
            response.EquippedId = snapshot.Account.EquippedNamecardId;
            response.CanAct = snapshot.CanAct;
            response.CatalogRevision = _shopCatalog.Revision;
            response.Offers = _shopCatalog.Products.Select(p =>
                new NamecardOfferMessage { Id = p.Id, Price = p.Price, Listed = p.Listed }).ToArray();
            return response;
        }

        public ulong RequestShop(ShopAction action = ShopAction.Read, string productId = null,
            string revision = null, string operationId = null)
        {
            if (!CanOpenShop)
                return 0;
            var requestId = ++_nextShopRequest;
            NetworkClient.Send(new NamecardShopRequest
            {
                RequestId = requestId, Action = action, ProductId = productId,
                CatalogRevision = revision, OperationId = operationId
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
            var visibleNames = new HashSet<string>(room.MemberNames);
            var game = _gameManager.GetGameInfo(room.Id);
            if (game != null)
                foreach (var player in game.Players)
                    if (player.IsOccupied) visibleNames.Add(player.PlayerName);
            foreach (var stale in room.Namecards.Keys.Where(name => !visibleNames.Contains(name)).ToArray())
                room.Namecards.Remove(stale);
            // Keep disconnected / departed hand participants' last public appearance until their hand ends.
            foreach (var name in room.MemberNames)
            {
                if (_playerManager.TryGetAccount(name, out var account))
                    room.Namecards[name] = account.EquippedNamecardId ?? string.Empty;
            }
            return room;
        }
    }
}
