using System.Collections;
using System.Linq;
using System.Reflection;
using Game.Network.Accounts;
using Game.SO;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Network.Diagnostics
{
    public sealed partial class PokerIntegrationRunner
    {
        private bool _testItems;

        // Only the explicit integration scenario uses this in-memory catalog; no production assets or grants.
        private void ConfigureIntegrationItems()
        {
            var catalog = new ItemServerCatalog { Products = new[]
                {
                    new ServerItemDefinition { Id = "integration-keepsake", Kind = ItemKind.NonConsumable, Price = 300, MaxQuantity = 1 }
                }};
            var registry = new ItemPurchaseRegistry();
            registry.Register("integration-keepsake", new ItemPurchasePolicy());
            typeof(NetworkRoom).GetField("_itemRuntime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_networkRoom, new ItemPurchaseRuntime(catalog, registry));
        }

        private IEnumerator WaitForItems(ShopPanel panel)
        {
            var until = Time.realtimeSinceStartup + 25f;
            while ((Field<ulong>(_networkRoom, "_collectionRequest") != 0 || Field<ItemShopRequest>(panel, "_pendingItem").RequestId != 0) && Time.realtimeSinceStartup < until)
                yield return null;
            if (!panel.ItemsView.Snapshot.CanAct) Fail("Item inventory did not load an actionable snapshot.");
        }

        private IEnumerator CheckItemsUI(ShopPanel panel, bool purchase)
        {
            panel.SelectTab(ShopTab.Items);
            yield return WaitForItems(panel);
            if (_finishing) yield break;
            var definition = ScriptableObject.CreateInstance<ItemDefinitionSO>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ItemDefinitionSO).GetField("_id", flags).SetValue(definition, "integration-keepsake");
            typeof(ItemDefinitionSO).GetField("_displayName", flags).SetValue(definition, "Integration keepsake");
            typeof(ItemDefinitionSO).GetField("_kind", flags).SetValue(definition, ItemKind.NonConsumable);
            var view = panel.ItemsView;
            var originalCatalog = (ItemShopCatalogSO)typeof(ItemBrowserView).GetField("_catalog", flags).GetValue(view);
            var catalog = ScriptableObject.CreateInstance<ItemShopCatalogSO>();
            var entry = new ItemShopCatalogSO.Entry();
            typeof(ItemShopCatalogSO.Entry).GetField("_item", flags).SetValue(entry, definition);
            typeof(ItemShopCatalogSO).GetField("_products", flags).SetValue(catalog,
                new System.Collections.Generic.List<ItemShopCatalogSO.Entry> { entry });
            typeof(ItemBrowserView).GetField("_catalog", flags).SetValue(view, catalog);
            view.SetSnapshot(view.Snapshot);
            view.SelectItem(definition.Id);
            if (purchase)
            {
                var before = GameController.Instance.State.Chips;
                if (before != 4000 || view.Snapshot.Balance != before) Fail("Namecard debit did not reach item balance.");
                Click(view, "_action");
                var box = FindObjectsOfType<MsgBox>().FirstOrDefault(p => p.Title == "购买物品");
                if (box == null) { Fail("Item confirmation missing."); yield break; }
                Click(box, "_cancelButton");
                if (view.Snapshot.QuantityOf(definition.Id) != 0 || GameController.Instance.State.Chips != before)
                    Fail("Cancel mutated inventory or balance.");
                Click(view, "_action");
                box = FindObjectsOfType<MsgBox>().First(p => p.Title == "购买物品");
                Click(box, "_confirmButton");
                if (view.Snapshot.QuantityOf(definition.Id) != 0 || Field<Button>(view, "_action").interactable)
                    Fail("Pending purchase mutated inventory or allowed duplicate submit.");
                yield return WaitForItems(panel);
                if (GameController.Instance.State.Chips != before - 300 || Field<uint>(panel, "_chips") != before - 300)
                    Fail("Item purchase did not synchronize account and namecard balance.");
            }
            if (view.Snapshot.QuantityOf(definition.Id) != 1 || Field<Button>(view, "_action").interactable)
                Fail("Permanent item was lost or could be purchased twice.");
            panel.Open(ShopPageMode.Collection);
            view.SelectItem(definition.Id);
            if (Field<Button>(view, "_action").interactable) Fail("Permanent item can be consumed.");
            panel.SelectTab(ShopTab.Namecards);
            WriteResult("items-" + _clientIndex + (purchase ? "-purchase" : "-reopen") + ".checked",
                "Actual item UI: quote, cancellation, authoritative purchase, shared balance, permanent inventory and reopen.");
            typeof(ItemBrowserView).GetField("_catalog", flags).SetValue(view, originalCatalog);
            Destroy(catalog);
            Destroy(definition);
        }
    }
}
