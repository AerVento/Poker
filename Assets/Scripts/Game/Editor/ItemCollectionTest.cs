using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.SO;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class ItemCollectionTest
    {
        private GameObject _root;
        private ShopPanel _panel;
        private ItemBrowserView _view;
        private ItemShopCatalogSO _shopCatalog;
        private readonly List<ItemDefinitionSO> _definitions = new List<ItemDefinitionSO>();
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Item collection test");
            _root.SetActive(false);
            _panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Panels/ShopPanel/ShopPanel.prefab"), _root.transform).GetComponent<ShopPanel>();
            _view = _panel.ItemsView;
            for (int i = 0; i < 8; i++)
            {
                var item = ScriptableObject.CreateInstance<ItemDefinitionSO>();
                Set(item, "_id", "test-" + i);
                Set(item, "_displayName", "测试物品 " + i);
                Set(item, "_kind", i == 1 ? ItemKind.NonConsumable : ItemKind.Consumable);
                _definitions.Add(item);
            }
            _shopCatalog = ScriptableObject.CreateInstance<ItemShopCatalogSO>();
            var entries = new List<ItemShopCatalogSO.Entry>();
            foreach (var definition in _definitions)
            {
                var entry = new ItemShopCatalogSO.Entry();
                Set(entry, "_item", definition);
                entries.Add(entry);
            }
            Set(_shopCatalog, "_products", entries);
            Set(_view, "_catalog", _shopCatalog);
            _panel.Open(ShopPageMode.Shop);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_shopCatalog);
            foreach (var definition in _definitions) Object.DestroyImmediate(definition);
            _definitions.Clear();
        }

        [Test]
        public void ContractEligibilityDisablesImmediatelyAndWaitsForFreshServerQuote()
        {
            _view.BindActions(_ => { });
            var conditions = new[] { new PurchaseConditionDescriptor(PurchaseConditionKind.Balance, ComparisonType.Equal, 0) };
            var offer = new ItemOffer("test-1", 0, true, 1000, conditions, true,
                Game.Network.Accounts.ItemShopResult.Success, 1);
            _view.SetSnapshot(new ItemInventorySnapshot(Array.Empty<ItemStack>(), new[] { offer }, 0, true));
            _view.SelectItem("test-1");
            Assert.IsTrue(Field<Button>(_view, "_action").interactable);
            var refreshes = 0;
            _view.EligibilityRefreshRequested += () => refreshes++;
            _view.SetBalance(1000);
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
            _view.SetBalance(0);
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
            Assert.AreEqual(2, refreshes);
            _view.SetSnapshot(new ItemInventorySnapshot(Array.Empty<ItemStack>(), new[] { offer }, 0, true));
            Assert.IsTrue(Field<Button>(_view, "_action").interactable);
            _view.SetSnapshot(new ItemInventorySnapshot(new[] { new ItemStack("test-1", 1) }, new[] { offer }, 0, true));
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
        }

        [Test]
        public void RepaymentButtonRequiresFreshServerEligibilityAndRespectsPayment()
        {
            _panel.Open(ShopPageMode.Collection);
            _view.BindActions(_ => { });
            var owned = new[] { new ItemStack("test-0", 1) };
            var offer = new ItemOffer("test-0", 0, canUse: true, useCost: 1100);
            _view.SetSnapshot(new ItemInventorySnapshot(owned, new[] { offer }, 1099, true));
            _view.SelectItem("test-0");
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
            Assert.AreEqual("无法使用", Field<TMP_Text>(_view, "_actionLabel").text);
            _view.SetSnapshot(new ItemInventorySnapshot(owned, new[] { offer }, 1100, true));
            Assert.IsTrue(Field<Button>(_view, "_action").interactable);
            _view.SetBalance(1101);
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
            _view.SetSnapshot(new ItemInventorySnapshot(owned, new[] { new ItemOffer("test-0", 0, canUse: false) }, 1101, true));
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
        }

        [Test]
        public void ServerEligibilityCanDisableAnOtherwiseLocallyEligibleContract()
        {
            _view.BindActions(_ => { });
            var offer = new ItemOffer("test-1", 0, true, 1000,
                new[] { new PurchaseConditionDescriptor(PurchaseConditionKind.Balance, ComparisonType.Equal, 0) },
                false, Game.Network.Accounts.ItemShopResult.OutsideRoomRequired, 1);
            _view.SetSnapshot(new ItemInventorySnapshot(Array.Empty<ItemStack>(), new[] { offer }, 0, true));
            _view.SelectItem("test-1");
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
        }

        [Test]
        public void BalanceRefreshDoesNotReplaceAnUnconfirmedPurchase()
        {
            _view.BindActions(_ => { });
            _view.SetSnapshot(Snapshot());
            Set(_view, "_pendingOperation", "purchase-operation");
            Set(_panel, "_online", true);
            Set(_panel, "_pendingItem", new Game.Network.ItemShopRequest {
                RequestId = 42, OperationId = "purchase-operation", Action = Game.Network.Accounts.ItemShopAction.Purchase });
            Set(_panel, "_itemDeadline", double.MaxValue);
            _view.EligibilityRefreshRequested += () => typeof(ShopPanel).GetMethod("QueueItemRefresh", Private).Invoke(_panel, null);
            _view.SetBalance(0);
            typeof(ShopPanel).GetMethod("UpdateItemsOnline", Private).Invoke(_panel, null);
            Assert.IsTrue(Field<bool>(_panel, "_itemRefreshNeeded"));
            Assert.AreEqual(42, Field<Game.Network.ItemShopRequest>(_panel, "_pendingItem").RequestId);
            Assert.AreEqual("purchase-operation", Field<string>(_view, "_pendingOperation"));
            Assert.IsFalse(_view.Snapshot.EligibilityCurrent);
            Assert.IsTrue(_view.CompleteAction("purchase-operation", Snapshot(1000), "confirmed"));
        }

        private ItemInventorySnapshot Snapshot(uint balance = 100) => new ItemInventorySnapshot(
            new[] { new ItemStack("test-0", 2), new ItemStack("test-1", 1), new ItemStack("test-2", 0) },
            _definitions.Select(item => new ItemOffer(item.Id, 100, canUse: item.Kind == ItemKind.Consumable)), balance, true);

        [TestCase(ShopPageMode.Shop)]
        [TestCase(ShopPageMode.Collection)]
        public void EachEntryDefaultsToItemsAndReusesTheSameNamecardView(ShopPageMode mode)
        {
            _panel.Open(mode);
            _panel.SelectTab(ShopTab.Namecards);
            var cards = _panel.GetComponentsInChildren<ShopItemCard>(true);
            _panel.SelectTab(ShopTab.Items);
            Assert.IsFalse(Field<GameObject>(_panel, "_namecardsRoot").activeSelf);
            _panel.SelectTab(ShopTab.Namecards);
            CollectionAssert.AreEqual(cards, _panel.GetComponentsInChildren<ShopItemCard>(true));
            _panel.Open(mode);
            Assert.AreEqual(ShopTab.Items, _panel.ActiveTab);
            Assert.AreEqual(mode, _view.Mode);
            Assert.IsTrue(_view.gameObject.activeSelf);
        }

        [Test]
        public void CollectionShowsPositiveOwnedStacksIncludingDelistedOrUnknownItems()
        {
            _panel.Open(ShopPageMode.Collection);
            _view.SetSnapshot(new ItemInventorySnapshot(new[] {
                new ItemStack("test-0", 3), new ItemStack("test-1", 0), new ItemStack("old-item", 4) }));
            var cards = Field<InventoryItemCard[]>(_view, "_cards");
            Assert.AreEqual(2, cards.Count(card => card.gameObject.activeSelf));
            _view.SelectItem("old-item");
            Assert.AreEqual("old-item", _view.SelectedId);
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
            Assert.AreEqual("当前拥有  × 4", Field<TMP_Text>(_view, "_detailQuantity").text);
            _view.SetSnapshot(ItemInventorySnapshot.Empty);
            Assert.IsNull(_view.SelectedId);
            Assert.IsTrue(Field<GameObject>(_view, "_empty").activeSelf);
        }

        [Test]
        public void ConsumablesNeedPositiveStockAndAHandlerWhilePermanentItemsCannotBeConsumed()
        {
            _panel.Open(ShopPageMode.Collection);
            _view.SetSnapshot(Snapshot());
            _view.SelectItem("test-0");
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
            _view.BindActions(request => { });
            Assert.IsTrue(Field<Button>(_view, "_action").interactable);
            _view.SelectItem("test-1");
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
            Assert.AreEqual("永久收藏", Field<TMP_Text>(_view, "_actionLabel").text);
            _view.SelectItem("test-0");
            _view.SetSnapshot(new ItemInventorySnapshot(new[] { new ItemStack("test-0", 0) }, canAct: true));
            Assert.IsNull(_view.SelectedId);
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
        }

        [Test]
        public void ShopHonorsQuotesAndPaginationClearsSelection()
        {
            _view.BindActions(request => { });
            _view.SetSnapshot(Snapshot(99));
            _view.SelectItem("test-0");
            Assert.AreEqual("无法购买", Field<TMP_Text>(_view, "_actionLabel").text);
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
            _view.SetSnapshot(Snapshot(100));
            Assert.IsTrue(Field<Button>(_view, "_action").interactable);
            _view.GoToPage(100);
            Assert.AreEqual(1, _view.CurrentPage);
            Assert.IsNull(_view.SelectedId);
            Assert.IsFalse(Field<Button>(_view, "_next").interactable);
            _view.SetSnapshot(new ItemInventorySnapshot(offers: new[] { new ItemOffer("test-0", 100, false) }));
            Assert.AreEqual(0, _view.CurrentPage);
            Assert.IsTrue(Field<GameObject>(_view, "_empty").activeSelf);
        }

        [Test]
        public void PendingActionSurvivesTabsAndOnlyMatchingResultCanReplaceInventory()
        {
            _view.SetSnapshot(Snapshot());
            _view.BindActions(request => { });
            Set(_view, "_pendingOperation", "operation");
            _panel.Open(ShopPageMode.Collection);
            _view.SelectItem("test-0");
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
            Assert.IsFalse(_view.CompleteAction("old-operation", ItemInventorySnapshot.Empty, ""));
            Assert.AreEqual(2u, _view.Snapshot.QuantityOf("test-0"));
            Assert.IsTrue(_view.CompleteAction("operation", Snapshot(), "成功"));
            Assert.IsTrue(Field<Button>(_view, "_action").interactable);
            _view.ResetSession();
            Assert.AreEqual(0, _view.Snapshot.Items.Count);
            Assert.IsFalse(_view.CompleteAction("operation", Snapshot(), ""));
        }

        [Test]
        public void SnapshotCopiesInputsAndRejectsDuplicateOwnership()
        {
            var input = new[] { new ItemStack("item", 3) };
            var snapshot = new ItemInventorySnapshot(input);
            input[0] = new ItemStack("item", 0);
            Assert.AreEqual(3u, snapshot.QuantityOf("item"));
            Assert.Throws<ArgumentException>(() => new ItemInventorySnapshot(new[] {
                new ItemStack("item", 1), new ItemStack("item", 2) }));
        }

        [Test]
        public void SharedBalanceRefreshesBothItemAffordabilityAndNamecardHeader()
        {
            typeof(ShopPanel).GetMethod("EnableTabs", Private).Invoke(_panel, null);
            _view.BindActions(request => { });
            _view.SetSnapshot(Snapshot(100));
            _panel.SetAccountView("测试", 99, new[] { "default" }, "", true);
            _view.SelectItem("test-0");
            Assert.AreEqual(99u, _view.Snapshot.Balance);
            Assert.IsFalse(Field<Button>(_view, "_action").interactable);
            _view.SetSnapshot(Snapshot(100));
            Assert.AreEqual(100u, Field<uint>(_panel, "_chips"));
            Assert.AreEqual("$100", Field<TMP_Text>(_panel, "_balance").text);
            Assert.IsTrue(Field<Button>(_view, "_action").interactable);
            typeof(ShopPanel).GetMethod("DisableTabs", Private).Invoke(_panel, null);
        }

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    }
}
