using System;
using System.Linq;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using UnityEngine;

namespace Game.UI
{
    public sealed partial class ShopPanel
    {
        private ItemShopRequest _pendingItem;
        private string _itemCatalogRevision;
        private double _itemDeadline;
        private int _itemAttempts;
        private bool _itemRefreshNeeded;
        private bool _itemUncertain;
        private double _itemRefreshAt;

        private void BeginItemsOnline()
        {
            _itemsView.BindActions(SendItemAction);
            _itemsView.EligibilityRefreshRequested += QueueItemRefresh;
            _itemsView.SetFeedback("正在读取库存与商品价格…");
        }

        private void ResetItemsOnline()
        {
            _pendingItem = default;
            _itemCatalogRevision = null;
            _itemAttempts = 0;
            _itemsView.EligibilityRefreshRequested -= QueueItemRefresh;
            _itemRefreshNeeded = false;
            _itemUncertain = false;
        }

        private void QueueItemRefresh()
        {
            if (!_online) return;
            _itemRefreshNeeded = true;
            _itemRefreshAt = Time.realtimeSinceStartupAsDouble + 0.25d;
        }

        public void ReceiveAccountBalance(uint dollars)
        {
            if (!_online || !isActiveAndEnabled) return;
            _dollars = dollars;
            _itemsView.SetBalance(dollars);
            Redraw();
        }

        private void SendItemAction(ItemActionRequest request)
        {
            StartItemRequest(new ItemShopRequest
            {
                Action = request.Action == ItemAction.Purchase ? ItemShopAction.Purchase : ItemShopAction.Use,
                ItemId = request.ItemId, Quantity = request.Quantity, QuotedPrice = request.QuotedPrice,
                OperationId = request.OperationId, CatalogRevision = _itemCatalogRevision
            });
        }

        private void StartItemRequest(ItemShopRequest request)
        {
            _pendingItem = request;
            _itemAttempts = 0;
            SendPendingItem();
        }

        private void SendPendingItem()
        {
            _pendingItem.RequestId = (NetworkManager.singleton as NetworkRoom)?.RequestItems(_pendingItem) ?? 0;
            _itemAttempts++;
            _itemDeadline = Time.realtimeSinceStartupAsDouble + 8d;
            if (_pendingItem.RequestId == 0) StopItemRequest("连接不可用，请重新打开商城核对库存与余额。");
        }

        private void UpdateItemsOnline()
        {
            if (!_online || _itemUncertain) return;
            if (_pendingItem.RequestId == 0)
            {
                if (_itemRefreshNeeded && Time.realtimeSinceStartupAsDouble >= _itemRefreshAt)
                {
                    _itemRefreshNeeded = false;
                    (NetworkManager.singleton as NetworkRoom)?.LoadCollection(force: true);
                }
                return;
            }
            if (Time.realtimeSinceStartupAsDouble < _itemDeadline) return;
            if (_itemAttempts < 3)
            {
                _itemsView.SetFeedback("正在核对服务器结果，请稍候…");
                SendPendingItem();
            }
            else StopItemRequest("暂未收到结果，请重新打开商城核对库存与余额。");
        }

        private void StopItemRequest(string message)
        {
            _pendingItem.RequestId = 0;
            _itemUncertain = true;
            // Preserve pending operation until the panel closes; an uncertain write must not be resubmitted with a new ID.
            var snapshot = _itemsView.Snapshot;
            _itemsView.SetSnapshot(new ItemInventorySnapshot(snapshot.Items, snapshot.Offers, snapshot.Balance, false));
            _itemsView.SetFeedback(message);
        }

        public void ReceiveItems(ItemShopResponse response)
        {
            if (!_online || !isActiveAndEnabled || response.AccountId != _accountId || response.RequestId == 0 ||
                response.RequestId != _pendingItem.RequestId || response.OperationId != _pendingItem.OperationId) return;
            if (response.Result == ItemShopResult.RateLimited && _itemAttempts < 3)
            {
                _itemDeadline = Time.realtimeSinceStartupAsDouble + 0.3d;
                return;
            }
            if (!response.HasSnapshot)
            {
                StopItemRequest("请求未完成，请重新打开商城核对库存与余额。");
                return;
            }
            var action = _pendingItem.Action;
            _pendingItem = default;
            _itemRefreshNeeded = false;
            _itemCatalogRevision = response.CatalogRevision;
            var snapshot = ReadItemsSnapshot(response);
            _availableChips = response.AvailableChips;
            _canExchange = response.CanExchange;
            _itemsView.SetWallet(response.Dollars, response.AvailableChips, response.CanExchange, false);
            if (response.Result == ItemShopResult.InsufficientChips) CurrencyFeedback.ShowInsufficientFunds();
            var message = response.Result switch
            {
                ItemShopResult.Success => action == ItemShopAction.Purchase
                    ? response.HasOutcome && response.AppliedRewardDollars > 0
                        ? response.IsNewlyApplied ? $"购买成功，获得 ${response.AppliedRewardDollars:N0} 美金。"
                            : $"已核对原购买结果：${response.AppliedRewardDollars:N0} 美金已入账，本次未重复发放。"
                        : "购买已确认，库存与余额已更新。"
                    : action == ItemShopAction.Use ? "使用成功。" : string.Empty,
                ItemShopResult.AlreadyOwned => "你已拥有这件永久物品，无需重复购买。",
                ItemShopResult.InsufficientChips => "余额不足。",
                ItemShopResult.NotOwned => "你尚未拥有这件物品。",
                ItemShopResult.UnknownItem => "物品已下架或不可操作，商品列表已刷新。",
                ItemShopResult.CatalogChanged => "商品配置或价格已更新，请重新确认。",
                ItemShopResult.QuoteChanged => "余额或价格已变化，请重新确认支付。",
                ItemShopResult.OutsideRoomRequired => "当前场景无法操作这件物品。",
                ItemShopResult.RoomStartedRequired => "这件物品只能在已开始的牌局中使用。",
                ItemShopResult.UseLocationNotAllowed => "当前场景不能使用这件物品。",
                ItemShopResult.QuantityLimit => "持有数量已达到上限。",
                ItemShopResult.NotConsumable => "这件物品为永久收藏，不可消耗。",
                ItemShopResult.EffectUnavailable => "此物品暂未开放使用，库存未扣除。",
                ItemShopResult.BalanceConditionNotMet => response.HasFailedCondition
                    ? ReadCondition(response.FailedCondition).RequirementText() : "余额不满足购买条件。",
                ItemShopResult.BalanceLimit => "余额将超过上限。",
                ItemShopResult.UseConditionNotMet => "无法使用，尚未满足物品使用条件。",
                _ => "请求未完成，库存与余额已刷新。"
            };
            if (action == ItemShopAction.Read)
            {
                _itemsView.SetSnapshot(snapshot);
                _itemsView.SetFeedback(message);
            }
            else _itemsView.CompleteAction(response.OperationId, snapshot, message);
        }

        private void ApplyItems(ItemShopResponse response)
        {
            _itemCatalogRevision = response.CatalogRevision;
            _availableChips = response.AvailableChips;
            _canExchange = response.CanExchange;
            _itemsView.SetWallet(response.Dollars, response.AvailableChips, response.CanExchange, false);
            _itemsView.SetSnapshot(ReadItemsSnapshot(response));
            _itemsView.ClearLoadingFeedback();
        }

        private static ItemInventorySnapshot ReadItemsSnapshot(ItemShopResponse response)
        {
            return new ItemInventorySnapshot(
                (response.Items ?? Array.Empty<ItemStackMessage>()).Select(p => new ItemStack(p.ItemId, p.Quantity)),
                (response.Offers ?? Array.Empty<ItemOfferMessage>()).Select(p => new ItemOffer(p.ItemId, p.Price, p.Listed,
                    p.RewardDollars, (p.PurchaseConditions ?? Array.Empty<PurchaseConditionMessage>()).Select(ReadCondition),
                    p.CanPurchase, p.BlockReason, p.MaxQuantity, p.CanUse, p.UseCost)),
                response.Dollars, response.CanAct);
        }


        private static PurchaseConditionDescriptor ReadCondition(PurchaseConditionMessage condition)
            => new PurchaseConditionDescriptor(condition.Kind, condition.Comparison, condition.Threshold, condition.Version);
    }
}
