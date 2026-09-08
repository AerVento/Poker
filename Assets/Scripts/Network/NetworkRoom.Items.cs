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
        private ItemPurchaseRuntime _itemRuntime;
        public ItemPurchaseEvents ItemPurchaseEvents { get; } = new ItemPurchaseEvents(exception => Debug.LogException(exception));
        private readonly Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)> _itemRequests =
            new Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)>();
        private ulong _nextItemRequest;

        private void RegisterItemServerHandlers()
        {
            _itemRequests.Clear();
            var asset = Resources.Load<TextAsset>("SO/Shop/ItemServerCatalog");
            if (asset == null) throw new InvalidOperationException("Missing item server catalog.");
            _itemRuntime = ContractPurchaseRegistration.CreateRuntime(new ItemServerCatalog
            {
                Products = JsonSerializer.Deserialize<ServerItemDefinition[]>(asset.text)
            });
            RegisterServerHandler<ItemShopRequest>((conn, request) =>
            {
                var response = ProcessItemRequest(conn, request);
                if (response.HasSnapshot) conn.Send(new AccountBalanceSync { Chips = response.Chips });
                conn.Send(response);
            }, requireAuthentication: true);
        }

        internal ItemShopResponse ProcessItemRequest(NetworkConnectionToClient conn, ItemShopRequest request)
        {
            var response = new ItemShopResponse
            {
                RequestId = request.RequestId, Result = ItemShopResult.Unavailable
            };
            if (_serverStorageFailed || conn == null || !conn.isAuthenticated ||
                !_playerManager.TryGetSession(conn, out var session)) return response;
            response.AccountId = session.Account.AccountId;
            _itemRequests.TryGetValue(conn, out var previous);
            if (request.RequestId == 0 || request.RequestId <= previous.Sequence ||
                !Enum.IsDefined(typeof(ItemShopAction), request.Action) ||
                (request.CatalogRevision?.Length ?? 0) > 64 || (request.OperationId?.Length ?? 0) > 32 ||
                (request.ItemId?.Length ?? 0) > 48)
            {
                response.Result = ItemShopResult.InvalidRequest;
                return response;
            }
            response.OperationId = request.OperationId;
            var now = Time.realtimeSinceStartupAsDouble;
            _itemRequests[conn] = (request.RequestId, previous.Next);
            if (now < previous.Next)
            {
                response.Result = ItemShopResult.RateLimited;
                return response;
            }
            _itemRequests[conn] = (request.RequestId, now + 0.2d);
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository as IItemRepository;
            if (repository == null || _itemRuntime == null) return response;
            bool inRoom = _roomManager.WhichRoom(session.Account.UserName, out _) || session.Account.LastRoomId.HasValue;
            var execution = request.Action == ItemShopAction.Read ? new ItemChangeExecution(ItemShopResult.Success) :
                new ItemShopService(repository, _itemRuntime, ItemPurchaseEvents).ChangeItem(response.AccountId, request.OperationId,
                    request.Action, request.ItemId, request.Quantity, request.QuotedPrice, request.CatalogRevision, inRoom);
            response.Result = execution.Result;
            response.IsNewlyApplied = execution.IsNewlyApplied;
            response.HasOutcome = execution.Outcome != null;
            if (execution.Outcome != null)
            {
                response.AppliedRewardChips = execution.Outcome.RewardChips;
                response.HasFailedCondition = execution.Outcome.FailedCondition != null;
                if (response.HasFailedCondition) response.FailedCondition = ProjectCondition(execution.Outcome.FailedCondition);
                response.Effects = execution.Outcome.Effects.Select(e => new ItemEffectOutcomeMessage
                {
                    EffectIndex = e.EffectIndex, CommandIndex = e.CommandIndex, Kind = e.Kind, Version = e.Version,
                    Amount = e.Amount, BalanceBefore = e.BalanceBefore, BalanceAfter = e.BalanceAfter
                }).ToArray();
            }
            var snapshot = repository.ReadItems(response.AccountId);
            if (snapshot == null) return response;
            session.Account = snapshot.Account.Copy();
            response.HasSnapshot = true;
            response.Chips = snapshot.Account.Chips;
            response.Items = snapshot.Items.Select(p => new ItemStackMessage { ItemId = p.ItemId, Quantity = p.Quantity }).ToArray();
            response.CanAct = snapshot.CanAct && !inRoom;
            response.CatalogRevision = _itemRuntime.Revision;
            response.Offers = _itemRuntime.Products.Select(p =>
            {
                var owned = snapshot.Items.FirstOrDefault(stack => stack.ItemId == p.Id)?.Quantity ?? 0;
                var reason = ItemPurchaseRuntime.CheckPurchase(new PurchaseContext(response.AccountId, response.Chips, p, owned),
                    response.CanAct, out _);
                return new ItemOfferMessage
                {
                    ItemId = p.Id, Price = p.Price, Listed = p.Listed, MaxQuantity = p.MaxQuantity,
                    RewardChips = p.Policy.RewardChips, PurchaseConditions = p.Policy.ConditionDescriptors.Select(ProjectCondition).ToArray(),
                    CanPurchase = reason == ItemShopResult.Success, BlockReason = reason
                };
            }).ToArray();
            return response;
        }

        private static PurchaseConditionMessage ProjectCondition(PurchaseConditionDescriptor condition)
            => new PurchaseConditionMessage { Kind = condition.Kind, Comparison = condition.Comparison, Threshold = condition.Threshold, Version = condition.Version };

        public ulong RequestItems(ItemShopRequest request)
        {
            if (!CanOpenShop) return 0;
            request.RequestId = ++_nextItemRequest;
            NetworkClient.Send(request);
            return request.RequestId;
        }

        private void RegisterItemClientHandlers()
        {
            NetworkClient.RegisterHandler<ItemShopResponse>(response =>
            {
                if (CanOpenShop && response.AccountId == GameController.Instance.State.AccountId)
                    ShopPanel.Instance?.ReceiveItems(response);
            });
        }
    }
}
