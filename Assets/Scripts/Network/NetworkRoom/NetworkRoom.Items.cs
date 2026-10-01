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
            var definitions = ShopConfiguration.Server;
            _itemRuntime = ContractPurchaseRegistration.CreateRuntime(ShopConfiguration.CreateItemCatalog(definitions),
                definitions.Gifts.Select(gift => gift.ItemId));
            WarnUnconfiguredInventory();
            RegisterServerHandler<ItemShopRequest>((conn, request) =>
            {
                var response = ProcessItemRequest(conn, request);
                if (response.HasSnapshot) conn.Send(new AccountBalanceSync { Chips = response.Chips, Dollars = response.Dollars });
                conn.Send(response);
                if (request.Action != ItemShopAction.Read && response.HasSnapshot)
                    conn.Send(ReadCollection(conn));
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
            UpdateWalletHandAccess(session.Account);
            var execution = request.Action == ItemShopAction.Read ? new ItemChangeExecution(ItemShopResult.Success) :
                new ItemShopService(repository, _itemRuntime, ItemPurchaseEvents).ChangeItem(response.AccountId, request.OperationId,
                    request.Action, request.ItemId, request.Quantity, request.QuotedPrice, request.CatalogRevision, inRoom, request.AuthorizeChipConversion, request.AuthorizedShortfall);
            response.Result = execution.Result;
            if (execution.IsNewlyApplied)
            {
                var updated = WalletRepository?.ReadWallet(response.AccountId)?.Account;
                if (updated != null) SynchronizeWalletBalance(updated);
            }
            if (response.Result == ItemShopResult.ConversionRequired)
            {
                var current = WalletRepository?.ReadWallet(response.AccountId)?.Account;
                var product = _itemRuntime.Find(request.ItemId);
                var cost = request.Action == ItemShopAction.Purchase ? (ulong)(product?.Price ?? 0) * request.Quantity : product?.UsePolicy?.Cost ?? 0;
                if (current != null && cost > current.Dollars && cost - current.Dollars <= uint.MaxValue)
                    response.RequiredConversion = (uint)(cost - current.Dollars);
            }
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
            return PopulateItems(response, repository, session, inRoom);
        }

        private ItemShopResponse PopulateItems(ItemShopResponse response, IItemRepository repository, PlayerSession session, bool inRoom)
        {
            var snapshot = repository.ReadItems(response.AccountId);
            if (snapshot == null) return response;
            session.Account = snapshot.Account.Copy();
            response.HasSnapshot = true;
            response.Chips = snapshot.Account.Chips;
            response.Dollars = snapshot.Account.Dollars;
            var wallet = ReadCurrentWallet(snapshot.Account);
            response.AvailableChips = wallet?.AvailableChips ?? 0;
            response.CanExchange = wallet?.CanExchange ?? false;
            response.Items = snapshot.Items.Select(p => new ItemStackMessage { ItemId = p.ItemId, Quantity = p.Quantity }).ToArray();
            response.CanAct = snapshot.CanAct;
            response.CatalogRevision = _itemRuntime.Revision;
            response.Offers = _itemRuntime.Products.Select(p =>
            {
                var owned = snapshot.Items.FirstOrDefault(stack => stack.ItemId == p.Id)?.Quantity ?? 0;
                var buyingBalance = response.Dollars;
                if (response.CanExchange && p.Price > buyingBalance && p.Price - buyingBalance <= response.AvailableChips)
                    buyingBalance = p.Price;
                var useBalance = response.Dollars;
                var useCost = p.UsePolicy?.Cost ?? 0;
                if (response.CanExchange && useCost > useBalance && useCost - useBalance <= response.AvailableChips)
                    useBalance = useCost;
                var reason = ItemPurchaseRuntime.CheckPurchase(new PurchaseContext(response.AccountId, buyingBalance, p, owned, snapshot.Account.TotalAssets),
                    response.CanAct, out _);
                return new ItemOfferMessage
                {
                    ItemId = p.Id, Price = p.Price, Listed = p.Listed, MaxQuantity = p.MaxQuantity,
                    RewardChips = p.Policy.RewardChips, PurchaseConditions = p.Policy.ConditionDescriptors.Select(ProjectCondition).ToArray(),
                    CanPurchase = reason == ItemShopResult.Success, BlockReason = reason,
                    CanUse = ItemPurchaseRuntime.CheckUse(new ItemUseContext(response.AccountId, useBalance, p, owned),
                        !inRoom) == ItemShopResult.Success,
                    UseCost = p.UsePolicy?.Cost ?? 0
                };
            }).ToArray();
            return response;
        }

        private static PurchaseConditionMessage ProjectCondition(PurchaseConditionDescriptor condition)
            => new PurchaseConditionMessage { Kind = condition.Kind, Comparison = condition.Comparison, Threshold = condition.Threshold, Version = condition.Version };

        public ulong RequestItems(ItemShopRequest request)
        {
            if (!CanOpenShop) return 0;
            if (request.Action != ItemShopAction.Read) InvalidateCollection();
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
