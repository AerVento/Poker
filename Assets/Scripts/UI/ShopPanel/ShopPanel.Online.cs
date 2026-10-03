using System;
using System.Linq;
using Framework.UI;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using UnityEngine;

namespace Game.UI
{
    // One request pipeline for every tab: purchase, equip and use all return the same authoritative inventory snapshot.
    public sealed partial class ShopPanel
    {
        private const string LoadingMessage = "正在读取账户、库存与商品价格…";

        private struct PendingAction
        {
            public ulong RequestId;
            public ItemAction Action;
            public ShopTab Tab;
            public ItemPurchaseRequest Purchase;
            public ItemEquipRequest Equip;
            public ItemConsumeRequest Consume;
            public string OperationId => Action == ItemAction.Purchase ? Purchase.OperationId :
                Action == ItemAction.Equip ? Equip.OperationId : Consume.OperationId;
        }

        private NetworkRoom _collectionRoom;
        private bool _online;
        private string _accountId;
        private PendingAction _pending;
        private bool _hasPending;
        private double _deadline;
        private int _attempts;
        private bool _uncertain;
        private bool _refreshNeeded;
        private double _refreshAt;

        /// <summary>A write is in flight or its outcome is unknown; no other write may start.</summary>
        public bool IsBusy => _hasPending || _uncertain;

        public void BeginOnline()
        {
            if (_online) return;
            _online = true;
            _accountId = GameController.Instance.State.AccountId;
            PurchaseRequested += BuyNamecard;
            EquipRequested += Equip;
            _itemsView.BindActions(SendItemAction);
            if (_cardFacesView != null)
            {
                _cardFacesView.PurchaseRequested += BuyCardFace;
                _cardFacesView.EquipRequested += Equip;
            }
            SetFeedback(LoadingMessage);
            _itemsView.SetFeedback(LoadingMessage);
            _collectionRoom = NetworkManager.singleton as NetworkRoom;
            if (_collectionRoom != null)
            {
                _collectionRoom.CollectionReceived += ApplyCollection;
                _collectionRoom.LoadCollection();
            }
        }

        private void ResetOnline()
        {
            if (_collectionRoom != null) _collectionRoom.CollectionReceived -= ApplyCollection;
            _collectionRoom = null;
            if (_cardFacesView != null)
            {
                _cardFacesView.PurchaseRequested -= BuyCardFace;
                _cardFacesView.EquipRequested -= Equip;
            }
            _online = false;
            _accountId = null;
            _pending = default;
            _hasPending = false;
            _uncertain = false;
            _refreshNeeded = false;
            _attempts = 0;
        }

        private void QueueRefresh()
        {
            if (!_online) return;
            _refreshNeeded = true;
            _refreshAt = Time.realtimeSinceStartupAsDouble + 0.25d;
        }

        public void ReceiveAccountBalance(uint dollars)
        {
            if (!_online || !isActiveAndEnabled || dollars == _inventory.Dollars) return;
            SetInventory(_playerName, _inventory.WithWallet(dollars, _inventory.AvailableChips, _inventory.CanExchange));
            QueueRefresh();
        }

        private void BuyNamecard(string itemId) => StartPurchase(ShopTab.Namecards, itemId, 1, ClientCatalog.FindOffer(itemId)?.Price ?? 0,
            Guid.NewGuid().ToString("N"));
        private void BuyCardFace(string itemId) => StartPurchase(ShopTab.CardFaces, itemId, 1, ClientCatalog.FindOffer(itemId)?.Price ?? 0,
            Guid.NewGuid().ToString("N"));

        private void SendItemAction(ItemActionRequest request)
        {
            if (request.Action == ItemAction.Purchase)
                StartPurchase(ShopTab.Items, request.ItemId, request.Quantity, request.QuotedPrice, request.OperationId);
            else if (request.Action == ItemAction.Use)
                Submit(new PendingAction
                {
                    Action = ItemAction.Use, Tab = ShopTab.Items,
                    Consume = new ItemConsumeRequest
                    {
                        OperationId = request.OperationId, Method = ItemConsumeMethod.Use, CatalogRevision = _inventory.CatalogRevision,
                        Commands = new[] { new ItemConsumeCommandMessage { ItemId = request.ItemId, Quantity = 1, TargetSeatIndex = -1 } },
                    },
                });
        }

        private void StartPurchase(ShopTab tab, string itemId, uint quantity, uint quotedPrice, string operationId) =>
            Submit(new PendingAction
            {
                Action = ItemAction.Purchase, Tab = tab,
                Purchase = new ItemPurchaseRequest
                {
                    OperationId = operationId, ItemId = itemId, Quantity = quantity, QuotedPrice = quotedPrice,
                    CatalogRevision = _inventory.CatalogRevision,
                },
            });

        private void Equip(string itemId) => Submit(new PendingAction
        {
            Action = ItemAction.Equip,
            Tab = ClientCatalog.Current?.FindCardFace(itemId)?.ItemId == itemId ? ShopTab.CardFaces : ShopTab.Namecards,
            Equip = new ItemEquipRequest { OperationId = Guid.NewGuid().ToString("N"), ItemId = itemId },
        });

        private void Submit(PendingAction action)
        {
            if (!_online || IsBusy) return;
            CloseConfirmation();
            _pending = action;
            _hasPending = true;
            _attempts = 0;
            Send();
            Redraw();
            if (_cardFacesView != null) _cardFacesView.Redraw();
        }

        private void Send()
        {
            var room = NetworkManager.singleton as NetworkRoom;
            _pending.RequestId = room == null ? 0 : _pending.Action switch
            {
                ItemAction.Purchase => room.RequestItemPurchase(_pending.Purchase),
                ItemAction.Equip => room.RequestItemEquip(_pending.Equip),
                _ => room.RequestItemConsume(_pending.Consume),
            };
            _attempts++;
            _deadline = Time.realtimeSinceStartupAsDouble + 8d;
            if (_pending.RequestId == 0) StopUncertain("连接不可用，请重新打开商城核对库存与余额。");
        }

        private void Update()
        {
            if (!_online || _uncertain) return;
            if (!_hasPending)
            {
                if (_refreshNeeded && Time.realtimeSinceStartupAsDouble >= _refreshAt)
                {
                    _refreshNeeded = false;
                    (NetworkManager.singleton as NetworkRoom)?.LoadCollection(force: true);
                }
                return;
            }
            if (Time.realtimeSinceStartupAsDouble < _deadline) return;
            if (_attempts < 3)
            {
                ShowResult(_pending.Tab, _pending.OperationId, "正在核对服务器结果，请稍候…", null);
                Send();
            }
            else StopUncertain("暂未收到结果，请重新打开商城核对库存与余额。");
        }

        // An unanswered write keeps its operation ID until the panel closes, so it is never resubmitted under a new ID.
        private void StopUncertain(string message)
        {
            var tab = _pending.Tab;
            _hasPending = false;
            _uncertain = true;
            SetInventory(_playerName, _inventory.Locked());
            ShowResult(tab, null, message, null);
        }

        private void ApplyCollection(InventoryResponse response)
        {
            if (!_online || !isActiveAndEnabled) return;
            if (!response.Success)
            {
                SetFeedback("收藏读取失败，请重新打开重试。");
                _itemsView.SetFeedback("收藏读取失败，请重新打开重试。");
                return;
            }
            if (_hasPending || _uncertain) return;
            _refreshNeeded = false;
            SetInventory(GameController.Instance.State.UserName, ReadSnapshot(response.Snapshot));
            if (_feedback.text == LoadingMessage) SetFeedback(string.Empty);
            _itemsView.ClearLoadingFeedback();
        }

        public void ReceiveItemAction(ItemActionResponse response)
        {
            if (!_online || !isActiveAndEnabled || !_hasPending || response.AccountId != _accountId || response.RequestId == 0 ||
                response.RequestId != _pending.RequestId || response.OperationId != _pending.OperationId)
                return;
            if (response.Result == ItemActionResult.RateLimited && _attempts < 3)
            {
                _deadline = Time.realtimeSinceStartupAsDouble + 0.3d;
                return;
            }
            var pending = _pending;
            _hasPending = false;
            _pending = default;
            if (!response.HasSnapshot)
            {
                _uncertain = true;
                SetInventory(_playerName, _inventory.Locked());
                ShowResult(pending.Tab, pending.OperationId, "请求未完成，请重新打开商城核对库存与余额。", _inventory);
                return;
            }
            _refreshNeeded = false;
            var snapshot = ReadSnapshot(response.Snapshot);
            SetInventory(GameController.Instance.State.UserName, snapshot);
            if (response.Result == ItemActionResult.InsufficientDollars) CurrencyFeedback.ShowInsufficientFunds();
            ShowResult(pending.Tab, pending.OperationId, ResultText(pending, response), snapshot);
        }

        private void ShowResult(ShopTab tab, string operationId, string message, InventorySnapshot snapshot)
        {
            if (tab == ShopTab.Items)
            {
                if (snapshot == null || !_itemsView.CompleteAction(operationId, snapshot, message)) _itemsView.SetFeedback(message);
            }
            else if (tab == ShopTab.CardFaces && _cardFacesView != null) _cardFacesView.SetFeedback(message);
            else SetFeedback(message);
            if (_cardFacesView != null) _cardFacesView.Redraw();
        }

        private static string ResultText(PendingAction pending, ItemActionResponse response)
        {
            var cardFace = pending.Tab == ShopTab.CardFaces;
            return response.Result switch
            {
                ItemActionResult.Success => pending.Action switch
                {
                    ItemAction.Purchase when response.AppliedRewardDollars > 0 => response.IsNewlyApplied
                        ? $"购买成功，获得 ${response.AppliedRewardDollars:N0} 美金。"
                        : $"已核对原购买结果：${response.AppliedRewardDollars:N0} 美金已入账，本次未重复发放。",
                    ItemAction.Purchase => pending.Tab == ShopTab.Items ? "购买已确认，库存与余额已更新。" :
                        cardFace ? "已收藏，可选择装备。" : "已收藏。",
                    ItemAction.Equip => cardFace ? "已装备，所有扑克牌展示已更新。" : "已装备。",
                    ItemAction.Use => "使用成功。",
                    _ => string.Empty,
                },
                ItemActionResult.AlreadyOwned => "你已拥有这件物品，无需重复购买。",
                ItemActionResult.InsufficientDollars => "余额不足。",
                ItemActionResult.NotOwned => "你尚未拥有这件物品。",
                ItemActionResult.UnknownItem => "物品已下架或不可操作，商品列表已刷新。",
                ItemActionResult.CatalogChanged => "商品配置或价格已更新，请重新确认。",
                ItemActionResult.QuoteChanged => "余额或价格已变化，请重新确认。",
                ItemActionResult.OutsideRoomRequired => pending.Action == ItemAction.Equip
                    ? cardFace ? "牌局中无法更换牌面。" : "牌局中无法更换名片。" : "当前场景无法使用这件物品。",
                ItemActionResult.RoomStartedRequired => "这件物品只能在已开始的牌局中使用。",
                ItemActionResult.LocationNotAllowed => "当前场景不能使用这件物品。",
                ItemActionResult.QuantityLimit => "持有数量已达到上限。",
                ItemActionResult.NotConsumable => "这件物品为永久收藏，不可消耗。",
                ItemActionResult.MethodUnavailable => "这件物品不能直接使用。",
                ItemActionResult.NotEquippable => "这件物品不能装备。",
                ItemActionResult.PurchaseConditionNotMet => response.HasFailedCondition
                    ? ReadCondition(response.FailedCondition).RequirementText() : "不满足购买条件。",
                ItemActionResult.BalanceLimit => "余额将超过上限。",
                ItemActionResult.UseConditionNotMet => "无法使用，尚未满足物品使用条件。",
                _ => "请求未完成，请重新打开商城核对库存与余额。",
            };
        }

        public static InventorySnapshot ReadSnapshot(InventorySnapshotMessage message) => new InventorySnapshot(
            (message.Items ?? Array.Empty<ItemStackMessage>()).Select(i => new ItemStack(i.ItemId, i.Quantity)),
            (message.Offers ?? Array.Empty<ItemOfferStateMessage>()).Select(o => new ItemOfferState(o.ItemId, o.CanPurchase, o.BlockReason)),
            message.Usable, message.Dollars, message.AvailableChips, message.CanExchange, message.EquippedNamecard,
            message.EquippedCardFace, message.CanAct, message.CanEquip, message.CatalogRevision);

        private static PurchaseConditionDescriptor ReadCondition(PurchaseConditionMessage condition)
            => new PurchaseConditionDescriptor(condition.Kind, condition.Comparison, condition.Threshold, condition.Version);
    }
}
