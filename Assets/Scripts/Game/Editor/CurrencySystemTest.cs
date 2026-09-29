using System;
using System.IO;
using Game.Network.Accounts;
using Game.UI;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class CurrencySystemTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private PlayerAccount _account;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerCurrency-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            var service = new AccountService(_repository, new PasswordHasher(iterations: 1000));
            service.Initialize();
            _account = service.Register("CurrencyUser", "password123").Account;
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        private string CreateOpponent()
        {
            var service = new AccountService(_repository, new PasswordHasher(iterations: 1000));
            var opponent = service.Register("CurrencyOther", "password123").Account;
            _repository.ExchangeWallet(opponent.AccountId, Guid.NewGuid().ToString("N"), true, 10000);
            return opponent.AccountId;
        }

        private void Convert(uint amount)
            => Assert.AreEqual(WalletResult.Success, _repository.ExchangeWallet(_account.AccountId,
                Guid.NewGuid().ToString("N"), true, amount));

        private ShopResult BuyNamecard(uint price, string operation = null)
        {
            var catalog = new NamecardServerCatalog { Products = new[] {
                new NamecardOffer { Id = "default", Price = 0 }, new NamecardOffer { Id = "currency-test", Price = price } } };
            return _repository.ChangeNamecard(_account.AccountId, operation ?? Guid.NewGuid().ToString("N"),
                ShopAction.Purchase, "currency-test", catalog.Revision, catalog);
        }

        [Test]
        public void PurchaseConvertsOnlyShortfallAndRetryDoesNotDebitAgain()
        {
            Convert(8000);
            string operation = Guid.NewGuid().ToString("N");
            Assert.AreEqual(ShopResult.Success, BuyNamecard(5000, operation));
            Assert.AreEqual(ShopResult.Success, BuyNamecard(5000, operation));
            var account = _repository.FindById(_account.AccountId);
            Assert.AreEqual(0, account.Dollars);
            Assert.AreEqual(5000, account.Chips);
        }

        [Test]
        public void InsufficientCombinedFundsLeavesBothBalancesUnchanged()
        {
            Convert(8000);
            Assert.AreEqual(ShopResult.InsufficientChips, BuyNamecard(10001));
            var account = _repository.FindById(_account.AccountId);
            Assert.AreEqual(2000, account.Dollars);
            Assert.AreEqual(8000, account.Chips);
        }

        [Test]
        public void ActiveHandBlocksConversionButAllowsExistingDollars()
        {
            Convert(8000);
            Assert.AreEqual(HandStartResult.Started, _repository.BeginHand("currency-hand", 7,
                new[] { _account.AccountId, CreateOpponent() }, 100, out _));
            _repository.SetHandWalletAccess("currency-hand", _account.AccountId, 1000, false);
            Assert.AreEqual(ShopResult.InsufficientChips, BuyNamecard(3000));
            Assert.AreEqual(ShopResult.Success, BuyNamecard(1000));
            Assert.AreEqual(8000, _repository.FindById(_account.AccountId).Chips);
        }

        [Test]
        public void FoldedHandNeverSpendsCommittedChips()
        {
            Convert(8000);
            Assert.AreEqual(HandStartResult.Started, _repository.BeginHand("currency-fold", 7,
                new[] { _account.AccountId, CreateOpponent() }, 100, out _));
            _repository.SetHandWalletAccess("currency-fold", _account.AccountId, 6000, true);
            Assert.AreEqual(ShopResult.InsufficientChips, BuyNamecard(4001));
            Assert.AreEqual(ShopResult.Success, BuyNamecard(4000));
            Assert.AreEqual(6000, _repository.FindById(_account.AccountId).Chips);
        }

        [Test]
        public void CardFacePurchaseUsesSameAutomaticConversion()
        {
            Convert(10000);
            var catalog = new CardFaceServerCatalog { Products = new[] {
                new CardFaceOffer { Id = "default" }, new CardFaceOffer { Id = "currency-test", Price = 500 } } };
            Assert.AreEqual(ShopResult.Success, _repository.ChangeCardFace(_account.AccountId,
                Guid.NewGuid().ToString("N"), ShopAction.Purchase, "currency-test", catalog.Revision, catalog));
            var account = _repository.FindById(_account.AccountId);
            Assert.AreEqual(0, account.Dollars);
            Assert.AreEqual(9500, account.Chips);
        }

        [Test]
        public void BulkItemPurchaseUsesSameAutomaticConversion()
        {
            Convert(10000);
            var catalog = new ItemServerCatalog { Products = new[] {
                new ServerItemDefinition { Id = "currency-item", Kind = ItemKind.Consumable, Price = 300, MaxQuantity = 10 } } };
            var registry = new ItemPurchaseRegistry();
            registry.Register("currency-item", new ItemPurchasePolicy());
            var uses = new ItemUseRegistry();
            uses.Register("currency-item", new ItemUsePolicy(null, null, kind: ItemUseKind.Gift));
            var runtime = new ItemPurchaseRuntime(catalog, registry, uses);
            var operation = Guid.NewGuid().ToString("N");
            for (int i = 0; i < 2; i++)
                Assert.AreEqual(ItemShopResult.Success, _repository.ChangeItem(_account.AccountId, operation,
                    ItemShopAction.Purchase, "currency-item", 3, 300, runtime.Revision, runtime).Result);
            var account = _repository.FindById(_account.AccountId);
            Assert.AreEqual(0, account.Dollars);
            Assert.AreEqual(9100, account.Chips);
            Assert.AreEqual(3, _repository.ReadItems(_account.AccountId).Items[0].Quantity);
        }

        [Test]
        public void FailedRewardPlanRollsBackConversion()
        {
            Convert(10000);
            var catalog = new ItemServerCatalog { Products = new[] {
                new ServerItemDefinition { Id = "overflow-item", Kind = ItemKind.Consumable, Price = 300, MaxQuantity = 2 } } };
            var registry = new ItemPurchaseRegistry();
            registry.Register("overflow-item", new ItemPurchasePolicy(onPurchaseEffects: new[] { new FailingReward() }));
            var runtime = new ItemPurchaseRuntime(catalog, registry);
            Assert.Throws<InvalidOperationException>(() => _repository.ChangeItem(_account.AccountId,
                Guid.NewGuid().ToString("N"), ItemShopAction.Purchase, "overflow-item", 1, 300, runtime.Revision, runtime));
            var account = _repository.FindById(_account.AccountId);
            Assert.AreEqual(0, account.Dollars);
            Assert.AreEqual(10000, account.Chips);
            Assert.AreEqual(0, _repository.ReadItems(_account.AccountId).Items.Length);
        }

        private sealed class FailingReward : IPurchaseEffect
        {
            public PurchaseEffectDescriptor Descriptor => new PurchaseEffectDescriptor(PurchaseEffectKind.CreditChips, 10);
            public System.Collections.Generic.IReadOnlyList<PurchaseEffectCommand> Plan(PurchaseContext context)
                => throw new InvalidOperationException("Injected effect failure after conversion");
        }

        [Test]
        public void GiftAutoPurchaseAlsoConvertsAndRemainsIdempotent()
        {
            Convert(10000);
            var runtime = ContractPurchaseRegistration.CreateRuntime(Game.SO.ItemShopCatalogSO.LoadDefault().CreateServerCatalog());
            var id = Game.Network.Accounts.GiftItemIds.RottenEgg;
            var operation = Guid.NewGuid().ToString("N");
            for (int i = 0; i < 2; i++)
                Assert.AreEqual(GiftUseResult.Success, _repository.ConsumeGiftBatch(_account.AccountId, operation,
                    new[] { new GiftUseCommand(id, 1, 2) }, runtime).Result);
            var account = _repository.FindById(_account.AccountId);
            Assert.AreEqual(10000 - 2 * runtime.Find(id).Price, account.Chips);
            Assert.AreEqual(0, account.Dollars);
        }

        [TestCase(2000u, 5000u, true, 4000ul, CurrencyResult.Success, 2000u)]
        [TestCase(2000u, 5000u, false, 4000ul, CurrencyResult.InsufficientFunds, 0u)]
        [TestCase(2000u, 5000u, false, 1000ul, CurrencyResult.Success, 0u)]
        [TestCase(2000u, 5000u, true, 7001ul, CurrencyResult.InsufficientFunds, 0u)]
        [TestCase(uint.MaxValue, uint.MaxValue, true, 4294967296ul, CurrencyResult.BalanceLimit, 0u)]
        public void PaymentBoundaries(uint dollars, uint chips, bool canExchange, ulong price, CurrencyResult expected, uint conversion)
        {
            Assert.AreEqual(expected, CurrencySystem.PlanPayment(dollars, chips, canExchange, price, out var actual));
            Assert.AreEqual(conversion, actual);
        }

        [Test]
        public void ExchangeAndSignedMutationNeverOverflow()
        {
            Assert.AreEqual(CurrencyResult.BalanceLimit, CurrencySystem.PlanExchange(1, uint.MaxValue, uint.MaxValue,
                true, true, 1, out _, out _));
            Assert.AreEqual(CurrencyResult.InsufficientFunds, CurrencySystem.PlanBalanceChange(100, long.MinValue, out _));
            Assert.AreEqual(CurrencyResult.BalanceLimit, CurrencySystem.PlanBalanceChange(100, long.MaxValue, out _));
            Assert.AreEqual(CurrencyResult.Success, CurrencySystem.PlanBalanceChange(100, -100, out var value));
            Assert.AreEqual(0, value);
        }

        [Test]
        public void QuantityStepsSupportZeroFullUintAndBothBounds()
        {
            Assert.AreEqual(0, QuantityStepper.AdjustValue(500, -1000, 0, 10000));
            Assert.AreEqual(1, QuantityStepper.AdjustValue(1, -10, 1, 100));
            Assert.AreEqual(10000, QuantityStepper.AdjustValue(9500, 1000, 0, 10000));
            Assert.AreEqual(uint.MaxValue, QuantityStepper.AdjustValue(uint.MaxValue - 500L, 1000, 0, uint.MaxValue));
            Assert.AreEqual(0, QuantityStepper.AdjustValue(0, 1000, 0, 0));
        }
    }
}
