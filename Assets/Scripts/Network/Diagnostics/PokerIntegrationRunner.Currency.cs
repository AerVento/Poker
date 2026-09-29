using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Game.Network.Accounts;
using Mirror;
using UnityEngine;

namespace Game.Network.Diagnostics
{
    public sealed partial class PokerIntegrationRunner
    {
        private IEnumerator CheckCurrencyPurchases()
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (!_authenticated && Time.realtimeSinceStartup < deadline) yield return null;
            if (!_authenticated) { Fail("Currency authentication timed out."); yield break; }
            _networkRoom.ExchangeWallet(true, 8000);
            deadline = Time.realtimeSinceStartup + 10;
            while (GameController.Instance.State.Chips != 8000 && Time.realtimeSinceStartup < deadline) yield return null;
            if (GameController.Instance.State.Chips != 8000) { Fail("Initial currency exchange failed."); yield break; }
            ItemShopResponse last = default;
            NetworkClient.RegisterHandler<ItemShopResponse>(response => last = response);
            ulong request = _networkRoom.RequestItems(new ItemShopRequest { Action = ItemShopAction.Read });
            deadline = Time.realtimeSinceStartup + 10;
            while (last.RequestId != request && Time.realtimeSinceStartup < deadline) yield return null;
            if (!last.HasSnapshot) { Fail("Currency item catalog missing."); yield break; }
            var offer = last.Offers.First(p => p.ItemId == GiftItemIds.RottenEgg);
            if (offer.Price != 50) { Fail("Currency fixture expects egg price 50."); yield break; }
            var buy = new ItemShopRequest { Action = ItemShopAction.Purchase, ItemId = offer.ItemId, Quantity = 50,
                QuotedPrice = offer.Price, CatalogRevision = last.CatalogRevision, OperationId = Guid.NewGuid().ToString("N") };
            for (int attempt = 0; attempt < 2; attempt++)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                request = _networkRoom.RequestItems(buy);
                deadline = Time.realtimeSinceStartup + 10;
                while (last.RequestId != request && Time.realtimeSinceStartup < deadline) yield return null;
                if (last.RequestId != request || last.Result != ItemShopResult.Success || last.Dollars != 0 || last.Chips != 7500)
                { Fail("Automatic shortfall purchase/retry failed: " + last.Result); yield break; }
            }
            yield return new WaitForSecondsRealtime(0.3f);
            buy.Quantity = 201;
            buy.OperationId = Guid.NewGuid().ToString("N");
            request = _networkRoom.RequestItems(buy);
            deadline = Time.realtimeSinceStartup + 10;
            while (last.RequestId != request && Time.realtimeSinceStartup < deadline) yield return null;
            if (last.RequestId != request || last.Result != ItemShopResult.InsufficientChips || last.Chips != 7500 || last.Dollars != 0)
            { Fail("Insufficient purchase changed balances."); yield break; }
            typeof(NetworkRoom).GetMethod("RegisterItemClientHandlers", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_networkRoom, null);
            Framework.UI.UIManager.Instance.PushPanel<UI.RoomListPanel>();
            WriteResult("currency-" + _clientIndex + ".checked", "Exact shortfall, atomic failure and idempotent retry passed over the network.");
        }
    }
}
