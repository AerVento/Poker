using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.UI;
using Mirror;
using UnityEngine;

namespace Game.Network
{
    // The account's inventory snapshot, pushed after every change and cached by the client for the shop, collection and gift menu.
    public partial class NetworkRoom
    {
        private readonly ClientSnapshotCache<InventoryResponse> _collectionCache = new ClientSnapshotCache<InventoryResponse>(600d);
        private readonly Dictionary<NetworkConnectionToClient, (ulong Id, double Next)> _collectionRequests =
            new Dictionary<NetworkConnectionToClient, (ulong Id, double Next)>();
        private ulong _nextCollectionRequest;
        private ulong _collectionRequest;
        private ulong _collectionRevision;
        private ulong _serverCollectionRevision;
        private int _cacheSession;
        public event Action<InventoryResponse> CollectionReceived;

        private void RegisterCollectionServerHandlers()
        {
            _collectionRequests.Clear();
            RegisterServerHandler<InventoryRequest>((conn, request) =>
            {
                _collectionRequests.TryGetValue(conn, out var previous);
                var now = Time.realtimeSinceStartupAsDouble;
                if (request.RequestId == 0 || request.RequestId <= previous.Id || now < previous.Next) return;
                _collectionRequests[conn] = (request.RequestId, now + 0.2d);
                conn.Send(ReadCollection(conn, request.RequestId));
            }, requireAuthentication: true);
        }

        internal InventoryResponse ReadCollection(NetworkConnectionToClient conn, ulong requestId = 0)
        {
            var response = new InventoryResponse { RequestId = requestId, Revision = ++_serverCollectionRevision };
            if (_serverStorageFailed || conn == null || !conn.isAuthenticated ||
                !_playerManager.TryGetSession(conn, out var session)) return response;
            response.AccountId = session.Account.AccountId;
            response.Success = TryReadInventory(session, out response.Snapshot);
            return response;
        }

        public bool TryGetCollection(out InventoryResponse response)
        {
            return _collectionCache.TryGet(Time.realtimeSinceStartupAsDouble, out response) &&
                HasAuthenticatedClient && response.AccountId == GameController.Instance.State.AccountId;
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
                NetworkClient.Send(new InventoryRequest { RequestId = id });
                await UniTask.Delay(TimeSpan.FromSeconds(8), ignoreTimeScale: true, cancellationToken: this.GetCancellationTokenOnDestroy());
                if (session != _cacheSession || _collectionRequest != id) return;
            }
            _collectionRequest = 0;
            CollectionReceived?.Invoke(default);
        }

        private void RegisterCollectionClientHandlers()
        {
            ClearClientCaches();
            NetworkClient.RegisterHandler<InventoryResponse>(response =>
            {
                if (!HasAuthenticatedClient || response.AccountId != GameController.Instance.State.AccountId ||
                    response.Revision <= _collectionRevision || response.RequestId != 0 && response.RequestId != _collectionRequest) return;
                _collectionRevision = response.Revision;
                _collectionRequest = 0;
                if (response.Success)
                {
                    _collectionCache.Store(response, Time.realtimeSinceStartupAsDouble);
                    GameController.Instance.State.SetWallet(response.Snapshot.Dollars, response.Snapshot.Chips);
                    CardFaceAppearance.Equip(response.Snapshot.EquippedCardFace);
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
