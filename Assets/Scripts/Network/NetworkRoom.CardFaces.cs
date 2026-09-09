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
        private CardFaceServerCatalog _cardFaceShopCatalog;
        private readonly Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)> _cardFaceShopRequests =
            new Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)>();
        private ulong _nextCardFaceShopRequest;

        private void RegisterCardFaceShopServerHandlers()
        {
            _cardFaceShopRequests.Clear();
            _cardFaceShopCatalog = CardFaceCatalogLoader.LoadServer();
            RegisterServerHandler<CardFaceShopRequest>((conn, request) =>
            {
                var response = ProcessCardFaceShopRequest(conn, request);
                if (response.HasSnapshot)
                    conn.Send(new AccountBalanceSync { Chips = response.Chips });
                conn.Send(response);
            }, requireAuthentication: true);
        }

        internal CardFaceShopResponse ProcessCardFaceShopRequest(NetworkConnectionToClient conn, CardFaceShopRequest request)
        {
            var response = new CardFaceShopResponse { RequestId = request.RequestId, Result = ShopResult.Unavailable };
            if (_serverStorageFailed || conn == null || !conn.isAuthenticated ||
                !_playerManager.TryGetSession(conn, out var session))
                return response;
            response.AccountId = session.Account.AccountId;
            _cardFaceShopRequests.TryGetValue(conn, out var previous);
            if (request.RequestId == 0 || request.RequestId <= previous.Sequence ||
                !Enum.IsDefined(typeof(ShopAction), request.Action) ||
                (request.CatalogRevision?.Length ?? 0) > 64 ||
                (request.OperationId?.Length ?? 0) > 32 || (request.ProductId?.Length ?? 0) > 48)
            {
                response.Result = ShopResult.InvalidRequest;
                return response;
            }
            var now = Time.realtimeSinceStartupAsDouble;
            _cardFaceShopRequests[conn] = (request.RequestId, previous.Next);
            if (now < previous.Next)
            {
                response.Result = ShopResult.RateLimited;
                return response;
            }
            _cardFaceShopRequests[conn] = (request.RequestId, now + 0.2d);
            if (_roomManager.WhichRoom(session.Account.UserName, out _) || session.Account.LastRoomId.HasValue)
            {
                response.Result = ShopResult.OutsideRoomRequired;
                return response;
            }
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository as ICardFaceRepository;
            if (repository == null || _cardFaceShopCatalog == null)
                return response;
            response.Result = request.Action == ShopAction.Read ? ShopResult.Success :
                repository.ChangeCardFace(response.AccountId, request.OperationId, request.Action,
                    request.ProductId, request.CatalogRevision, _cardFaceShopCatalog);
            var snapshot = repository.ReadCardFaces(response.AccountId);
            if (snapshot == null)
                return response;
            session.Account = snapshot.Account.Copy();
            response.HasSnapshot = true;
            response.Chips = snapshot.Account.Chips;
            response.OwnedIds = snapshot.OwnedIds;
            response.EquippedId = snapshot.Account.EquippedCardFaceId;
            response.CanAct = snapshot.CanAct;
            response.CatalogRevision = _cardFaceShopCatalog.Revision;
            response.Offers = _cardFaceShopCatalog.Products.Select(p =>
                new CardFaceOfferMessage { Id = p.Id, Price = p.Price, Listed = p.Listed }).ToArray();
            return response;
        }

        public ulong RequestCardFaceShop(ShopAction action = ShopAction.Read, string productId = null,
            string revision = null, string operationId = null)
        {
            if (!CanOpenShop)
                return 0;
            var requestId = ++_nextCardFaceShopRequest;
            NetworkClient.Send(new CardFaceShopRequest
            {
                RequestId = requestId, Action = action, ProductId = productId,
                CatalogRevision = revision, OperationId = operationId
            });
            return requestId;
        }

        private void RegisterCardFaceShopClientHandlers()
        {
            NetworkClient.RegisterHandler<CardFaceShopResponse>(response =>
            {
                if (HasAuthenticatedClient && response.AccountId == GameController.Instance.State.AccountId)
                {
                    if (response.HasSnapshot) CardFaceAppearance.Equip(response.EquippedId);
                    if (CanOpenShop) ShopPanel.Instance?.ReceiveCardFaces(response);
                }
            });
        }

    }
}
