using System;
using System.Collections.Generic;
using Game.Network;
using Game.Network.Accounts;
using Game.SO;
using Mirror;
using UnityEngine;

namespace Game.UI
{
    public sealed partial class ShopPanel
    {
        private bool _online;
        private string _accountId;
        private string _catalogRevision;
        private readonly Dictionary<string, NamecardOfferMessage> _offers = new Dictionary<string, NamecardOfferMessage>();
        private NamecardShopRequest _pending;
        private double _deadline;
        private int _attempts;

        public void BeginOnline()
        {
            if (_online) return;
            _online = true;
            _accountId = GameController.Instance.State.AccountId;
            PurchaseRequested += BuyOnline;
            EquipRequested += EquipOnline;
            SetFeedback("正在读取账户、库存与商品价格…");
            StartRequest(ShopAction.Read, null);
            BeginItemsOnline();
        }

        private void ResetOnline()
        {
            ResetItemsOnline();
            _online = false;
            _accountId = null;
            _pending = default;
            _offers.Clear();
            _catalogRevision = null;
        }

        private uint PriceOf(NamecardProductSO product) => _online && _offers.TryGetValue(product.Id, out var offer)
            ? offer.Price : product.Price;

        private bool IsVisible(NamecardProductSO product) => (product.IsListed && !product.CanPurchase) || product.IsDefault || _owned.Contains(product.Id) ||
            (_online ? _offers.TryGetValue(product.Id, out var offer) && offer.Listed : product.IsListed);

        private bool HasOffer(NamecardProductSO product) => product.CanPurchase && (!_online || _offers.ContainsKey(product.Id));

        private void BuyOnline(NamecardProductSO product) => StartRequest(ShopAction.Purchase, product.Id);
        private void EquipOnline(string id) => StartRequest(ShopAction.Equip, string.IsNullOrEmpty(id) ? "default" : id);

        private void StartRequest(ShopAction action, string productId)
        {
            CloseConfirmation();
            _canAct = false;
            _attempts = 0;
            _pending = new NamecardShopRequest
            {
                Action = action, ProductId = productId, CatalogRevision = _catalogRevision,
                OperationId = action == ShopAction.Read ? null : Guid.NewGuid().ToString("N")
            };
            SendPending();
            Redraw();
        }

        private void SendPending()
        {
            var room = NetworkManager.singleton as NetworkRoom;
            _pending.RequestId = room?.RequestShop(_pending.Action, _pending.ProductId,
                _pending.CatalogRevision, _pending.OperationId) ?? 0;
            _attempts++;
            _deadline = Time.realtimeSinceStartupAsDouble + 8d;
            if (_pending.RequestId == 0)
                SetFeedback("请返回大厅并确认连接后重试。");
        }

        private void Update()
        {
            UpdateItemsOnline();
            if (!_online || _pending.RequestId == 0 || Time.realtimeSinceStartupAsDouble < _deadline)
                return;
            if (_attempts < 3)
            {
                SetFeedback("正在核对服务器结果，请稍候…");
                SendPending();
            }
            else
            {
                _pending.RequestId = 0;
                SetFeedback("暂未收到结果，请重新打开商城核对库存与余额。");
            }
        }

        public void Receive(NamecardShopResponse response)
        {
            if (!_online || !isActiveAndEnabled || response.AccountId != _accountId ||
                response.RequestId == 0 || response.RequestId != _pending.RequestId)
                return;
            if (response.Result == ShopResult.RateLimited && _attempts < 3)
            {
                _deadline = Time.realtimeSinceStartupAsDouble + 0.3d;
                return;
            }
            var action = _pending.Action;
            _pending.RequestId = 0;
            if (response.HasSnapshot)
            {
                _offers.Clear();
                foreach (var offer in response.Offers ?? Array.Empty<NamecardOfferMessage>())
                    if (offer.Id != null) _offers[offer.Id] = offer;
                _catalogRevision = response.CatalogRevision;
                SetAccountView(GameController.Instance.State.UserName, response.Chips, response.OwnedIds,
                    response.EquippedId, response.CanAct);
            }
            SetFeedback(response.Result switch
            {
                ShopResult.Success => !response.CanAct ? "离开房间并完成当前牌局后才能购买或装备。" :
                    action == ShopAction.Purchase ? "已永久收藏，点击装备即可使用。" :
                    action == ShopAction.Equip ? "已装备，下次进入房间即可展示。" : "永久收藏 · 自由换装",
                ShopResult.AlreadyOwned => "你已拥有这张名片，无需重复购买。",
                ShopResult.InsufficientChips => "余额不足，账户余额已刷新。",
                ShopResult.NotOwned => "请先购买这张名片。",
                ShopResult.CatalogChanged => "商品价格或上架状态已更新，请重新确认。",
                ShopResult.UnknownProduct => "这张名片已不可购买，请刷新商城。",
                ShopResult.OutsideRoomRequired => "请先离开房间并完成当前牌局。",
                _ => "请求未完成，请重新打开商城核对库存与余额。"
            });
        }
    }
}
