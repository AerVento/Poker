using System;
using System.Collections.Generic;
using Framework.UI;
using Game.Network;
using Game.Network.Accounts;
using Game.SO;
using Mirror;
using UnityEngine;

namespace Game.UI
{
    public sealed partial class ShopPanel
    {
        private NetworkRoom _collectionRoom;
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
            BeginItemsOnline();
            _collectionRoom = NetworkManager.singleton as NetworkRoom;
            if (_collectionRoom != null)
            {
                _collectionRoom.CollectionReceived += ApplyCollection;
                _collectionRoom.LoadCollection();
            }
            if (ActiveTab == ShopTab.CardFaces) _cardFacesView?.BeginOnline();
        }

        private void ResetOnline()
        {
            if (_collectionRoom != null) _collectionRoom.CollectionReceived -= ApplyCollection;
            _collectionRoom = null;
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

        private bool HasOffer(NamecardProductSO product) => !_online || _offers.ContainsKey(product.Id);

        private void BuyOnline(NamecardProductSO product) => StartRequest(ShopAction.Purchase, product.Id);
        private void EquipOnline(string id) => StartRequest(ShopAction.Equip, string.IsNullOrEmpty(id) ? "default" : id);

        private void StartRequest(ShopAction action, string productId, bool authorizeChipConversion = false,
            uint authorizedShortfall = 0)
        {
            CloseConfirmation();
            _canAct = false;
            _attempts = 0;
            _pending = new NamecardShopRequest
            {
                Action = action, ProductId = productId, CatalogRevision = _catalogRevision,
                OperationId = action == ShopAction.Read ? null : Guid.NewGuid().ToString("N"),
                AuthorizeChipConversion = authorizeChipConversion,
                AuthorizedShortfall = authorizedShortfall
            };
            SendPending();
            Redraw();
        }

        private void SendPending()
        {
            var room = NetworkManager.singleton as NetworkRoom;
            _pending.RequestId = room?.RequestShop(_pending.Action, _pending.ProductId,
                _pending.CatalogRevision, _pending.OperationId, _pending.AuthorizeChipConversion,
                _pending.AuthorizedShortfall) ?? 0;
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

        private void ApplyCollection(CollectionResponse response)
        {
            if (!_online || !isActiveAndEnabled) return;
            if (!response.Success)
            {
                SetFeedback("收藏读取失败，请重新打开重试。");
                _itemsView.SetFeedback("收藏读取失败，请重新打开重试。");
                return;
            }
            if (_pending.RequestId == 0) ApplyNamecards(response.Namecards);
            if (_pendingItem.RequestId == 0)
            {
                _itemRefreshNeeded = false;
                ApplyItems(response.Items);
            }
            _cardFacesView?.ApplyCollection(response.CardFaces);
        }

        private void ApplyNamecards(NamecardShopResponse response)
        {
            _offers.Clear();
            foreach (var offer in response.Offers ?? Array.Empty<NamecardOfferMessage>())
                if (offer.Id != null) _offers[offer.Id] = offer;
            _catalogRevision = response.CatalogRevision;
            SetAccountView(GameController.Instance.State.UserName, response.Dollars, response.OwnedIds,
                response.EquippedId, response.CanAct, response.AvailableChips, response.CanExchange,
                response.CanEquip);
            if (_feedback.text == "正在读取账户、库存与商品价格…") SetFeedback(string.Empty);
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
            _pending = default;
            if (response.HasSnapshot)
            {
                ApplyNamecards(response);
            }
            if (response.Result == ShopResult.InsufficientChips) CurrencyFeedback.ShowInsufficientFunds();
            SetFeedback(response.Result switch
            {
                ShopResult.Success => action == ShopAction.Purchase ? "已收藏。" :
                    action == ShopAction.Equip ? "已装备。" : string.Empty,
                ShopResult.AlreadyOwned => "你已拥有这张名片，无需重复购买。",
                ShopResult.InsufficientChips => "余额不足。",
                ShopResult.NotOwned => "请先购买这张名片。",
                ShopResult.CatalogChanged => "商品价格或上架状态已更新，请重新确认。",
                ShopResult.QuoteChanged => "余额或价格已变化，请重新确认购买。",
                ShopResult.UnknownProduct => "这张名片已不可购买，请刷新商城。",
                ShopResult.OutsideRoomRequired => "牌局中无法更换名片。",
                _ => "请求未完成，请重新打开商城核对库存与余额。"
            });
        }


    }
}
