using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Network.Accounts;
using Game.UI;
using Mirror;
using UnityEngine;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        private readonly ClientSnapshotCache<CollectionResponse> _collectionCache = new ClientSnapshotCache<CollectionResponse>(600d);
        private readonly Dictionary<NetworkConnectionToClient, (ulong Id, double Next)> _collectionRequests =
            new Dictionary<NetworkConnectionToClient, (ulong Id, double Next)>();
        private ulong _nextCollectionRequest;
        private ulong _collectionRequest;
        private ulong _collectionRevision;
        private ulong _serverCollectionRevision;
        private int _cacheSession;
        public event Action<CollectionResponse> CollectionReceived;

        private void RegisterCollectionServerHandlers()
        {
            _collectionRequests.Clear();
            RegisterServerHandler<CollectionRequest>((conn, request) =>
            {
                _collectionRequests.TryGetValue(conn, out var previous);
                var now = Time.realtimeSinceStartupAsDouble;
                if (request.RequestId == 0 || request.RequestId <= previous.Id || now < previous.Next) return;
                _collectionRequests[conn] = (request.RequestId, now + 0.2d);
                conn.Send(ReadCollection(conn, request.RequestId));
            }, requireAuthentication: true);
        }

        internal CollectionResponse ReadCollection(NetworkConnectionToClient conn, ulong requestId = 0)
        {
            var response = new CollectionResponse { RequestId = requestId, Revision = ++_serverCollectionRevision };
            if (_serverStorageFailed || conn == null || !conn.isAuthenticated ||
                !_playerManager.TryGetSession(conn, out var session)) return response;
            response.AccountId = session.Account.AccountId;
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository;
            if (!(repository is IItemRepository items) || !(repository is INamecardRepository namecards) ||
                !(repository is ICardFaceRepository faces) || _itemRuntime == null || _shopCatalog == null || _cardFaceShopCatalog == null)
                return response;
            bool inRoom = _roomManager.WhichRoom(session.Account.UserName, out _) || session.Account.LastRoomId.HasValue;
            response.Items = PopulateItems(new ItemShopResponse { AccountId = response.AccountId, Result = ItemShopResult.Success }, items, session, inRoom);
            response.Namecards = PopulateNamecardSnapshot(new NamecardShopResponse { AccountId = response.AccountId, Result = ShopResult.Success }, namecards, session);
            response.CardFaces = PopulateCardFaces(new CardFaceShopResponse { AccountId = response.AccountId, Result = ShopResult.Success }, faces, session);
            response.Success = response.Items.HasSnapshot && response.Namecards.HasSnapshot && response.CardFaces.HasSnapshot;
            return response;
        }

        public bool TryGetCollection(out CollectionResponse response)
        {
            return _collectionCache.TryGet(Time.realtimeSinceStartupAsDouble, out response) &&
                HasAuthenticatedClient && response.AccountId == GameController.Instance.State.AccountId &&
                response.Items.CanAct && response.Namecards.CanAct && response.CardFaces.CanAct;
        }

        public void LoadCollection(bool force = false)
        {
            if (!CanOpenShop) return;
            if (!force && TryGetCollection(out var cached))
            {
                CollectionReceived?.Invoke(cached);
                return;
            }
            if (_collectionRequest != 0) return;
            LoadCollectionAsync(_cacheSession).Forget();
        }

        private async UniTaskVoid LoadCollectionAsync(int session)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (this == null || session != _cacheSession) return;
                if (!CanOpenShop) { _collectionRequest = 0; return; }
                var id = _collectionRequest = ++_nextCollectionRequest;
                NetworkClient.Send(new CollectionRequest { RequestId = id });
                await UniTask.Delay(TimeSpan.FromSeconds(8), ignoreTimeScale: true, cancellationToken: this.GetCancellationTokenOnDestroy());
                if (session != _cacheSession || _collectionRequest != id) return;
            }
            _collectionRequest = 0;
            CollectionReceived?.Invoke(default);
        }

        private void RegisterCollectionClientHandlers()
        {
            ClearClientCaches();
            NetworkClient.RegisterHandler<CollectionResponse>(response =>
            {
                if (!HasAuthenticatedClient || response.AccountId != GameController.Instance.State.AccountId ||
                    response.Revision <= _collectionRevision || response.RequestId != 0 && response.RequestId != _collectionRequest) return;
                _collectionRevision = response.Revision;
                _collectionRequest = 0;
                if (response.Success)
                {
                    _collectionCache.Store(response, Time.realtimeSinceStartupAsDouble);
                    GameController.Instance.State.SetWallet(response.Items.Dollars, response.Items.Chips);
                    CardFaceAppearance.Equip(response.CardFaces.EquippedId);
                }
                else _collectionCache.Clear();
                CollectionReceived?.Invoke(response);
            });
        }

        private void InvalidateCollection()
        {
            _collectionCache.Clear();
            _collectionRequest = 0;
        }

        private void ClearClientCaches()
        {
            _cacheSession++;
            InvalidateCollection();
            _collectionRevision = 0;
            ClearLeaderboardCache();
        }
    }
}
