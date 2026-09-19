using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Achievements;
using Game.Network;
using Game.Network.Accounts;
using Game.Network.Achievements;
using Game.SO;
using Game.UI;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class RewardNamecardTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private AchievementService _service;
        private NamecardServerCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerRewardNamecard-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            var accounts = new AccountService(_repository, new PasswordHasher(iterations: 1000));
            accounts.Initialize();
            accounts.Register("RewardAlice", "password123");
            accounts.Register("RewardBob", "password123");
            _catalog = NamecardCatalogSO.LoadDefault().CreateServerCatalog();
            var registry = new ItemIdentityRegistry(_catalog, new ItemServerCatalog());
            _service = new AchievementService(_repository, new AchievementCatalog(new Achievement[] {
                new ShowdownStraightFlushAchievement(), new RoyalStraightFlushAchievement() }),
                id => registry.TryGet(id, out var item) ? (uint?)item.MaxQuantity : null);
            _service.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        private string Alice => _repository.FindByUserName("RewardAlice").AccountId;
        private string Bob => _repository.FindByUserName("RewardBob").AccountId;

        private void Publish(bool royal, GameStage stage = GameStage.RoundOver, bool folded = false, bool showdown = true)
        {
            var players = new[] {
                new AchievementPlayer(Alice, "RewardAlice", 0, folded, false, false, 10000, 10000, 0, 0,
                    Array.Empty<Poker>(), PokerHandRank.Straight_Flush, new[] { (int)(royal ? PokerRank.Ace : PokerRank.Nine) }, 0, 0),
                new AchievementPlayer(Bob, "RewardBob", 1, false, false, false, 10000, 10000, 0, 0,
                    Array.Empty<Poker>(), PokerHandRank.Pair, new[] { (int)PokerRank.Two }, 0, 0) };
            var hand = "reward-" + royal + "-" + stage + "-" + folded + "-" + showdown;
            var context = new AchievementContext(hand + ":achievement:" + (int)stage, hand, 1, GameStage.River,
                stage, 0, 0, 1, 50, showdown, 1000,
                new[] { new AchievementStreet(stage, 0, Array.Empty<Poker>(), players) },
                Array.Empty<AchievementAction>(), Array.Empty<AchievementPot>());
            _repository.PublishAchievementEvent(context);
            _service.DrainEvents();
        }

        private long Sql(string sql)
        {
            using var connection = new SqliteConnection("Data Source=" + _path);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        private ShopResult Change(ShopAction action, string id) => _repository.ChangeNamecard(Alice,
            Guid.NewGuid().ToString("N"), action, id, _catalog.Revision, _catalog);

        [TestCase(false, "straight-flush", "showdown_straight_flush", 20000u)]
        [TestCase(true, "royal-straight-flush", "royal_straight_flush", 50000u)]
        public void ClaimPersistsCosmeticAndChipsExactlyOnceAndAllowsEquip(bool royal, string id, string achievementId, uint chips)
        {
            Assert.AreEqual(ShopResult.UnknownProduct, Change(ShopAction.Purchase, id));
            Assert.AreEqual(ShopResult.NotOwned, Change(ShopAction.Equip, id));
            Assert.AreEqual(AchievementResult.NotComplete, _service.Claim(Alice, achievementId, out _).Result);
            Publish(royal);
            Publish(royal);
            Assert.AreEqual(AchievementResult.NotComplete, _service.Claim(Bob, achievementId, out _).Result);
            var claim = _service.Claim(Alice, achievementId, out var account);
            Assert.AreEqual(AchievementResult.Success, claim.Result);
            Assert.AreEqual(10000u + chips, account.Chips);
            Assert.AreEqual("namecard-" + id, claim.GrantedItems.Single().ItemId);
            Assert.IsEmpty(account.EquippedNamecardId);
            Assert.AreEqual(AchievementResult.AlreadyClaimed, _service.Claim(Alice, achievementId, out _).Result);
            Assert.AreEqual(ShopResult.UnknownProduct, Change(ShopAction.Purchase, id));
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Equip, id));
            var reopened = new SqliteAccountRepository(_path);
            reopened.Initialize();
            CollectionAssert.Contains(reopened.ReadNamecards(Alice).OwnedIds, id);
            Assert.AreEqual(id, reopened.FindById(Alice).EquippedNamecardId);
            Assert.AreEqual(10000u + chips, reopened.FindById(Alice).Chips);
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM account_items WHERE item_id LIKE 'namecard-%';"));
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='AchievementReward';"));
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Equip, "default"));
            Assert.IsTrue(reopened.ReconcileChips().IsConsistent);
        }

        [Test]
        public void AlreadyOwnedCosmeticDoesNotBlockRemainingReward()
        {
            var registry = new ItemIdentityRegistry(_catalog, new ItemServerCatalog());
            Assert.AreEqual(ItemGrantResult.Success, _repository.GrantItem(Alice, "namecard-straight-flush",
                Guid.NewGuid().ToString("N"), 1, registry).Result);
            Publish(false);
            Assert.AreEqual(AchievementResult.Success, _service.Claim(Alice, "showdown_straight_flush", out var account).Result);
            Assert.AreEqual(30000, account.Chips);
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM account_namecards;"));
        }

        [Test]
        public void FailedClaimRollsBackChipsCosmeticAndClaimStatusTogether()
        {
            Publish(false);
            Sql("CREATE TRIGGER fail_reward_save BEFORE UPDATE ON achievement_accounts BEGIN SELECT RAISE(ABORT,'test rollback'); END;");
            Assert.Throws<SqliteException>(() => _service.Claim(Alice, "showdown_straight_flush", out _));
            Assert.AreEqual(10000, _repository.FindById(Alice).Chips);
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM account_namecards;"));
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='AchievementReward';"));
            Assert.AreEqual(0, _service.GetStatus(Alice).Achievements.Single(a => a.Id == "showdown_straight_flush").ClaimedAt);
            Sql("DROP TRIGGER fail_reward_save;");
            Assert.AreEqual(AchievementResult.Success, _service.Claim(Alice, "showdown_straight_flush", out _).Result);
        }

        [Test]
        public void FoldedHandsDoNotUnlockAndRoyalAlsoQualifiesForShowdownStraightFlush()
        {
            Publish(true, folded: true);
            Assert.IsTrue(_service.GetStatus(Alice).Achievements.All(a => a.CompletedAt == 0));
            Publish(false, GameStage.River, showdown: false);
            Assert.IsTrue(_service.GetStatus(Alice).Achievements.All(a => a.CompletedAt == 0));
            Publish(true, GameStage.River, showdown: false);
            Assert.Greater(_service.GetStatus(Alice).Achievements.Single(a => a.Id == "royal_straight_flush").CompletedAt, 0);
            Assert.AreEqual(0, _service.GetStatus(Alice).Achievements.Single(a => a.Id == "showdown_straight_flush").CompletedAt);
            Publish(true);
            foreach (var id in new[] { "showdown_straight_flush", "royal_straight_flush" })
                Assert.AreEqual(AchievementResult.Success, _service.Claim(Alice, id, out _).Result);
            Assert.AreEqual(80000, _repository.FindById(Alice).Chips);
            Assert.AreEqual(2, Sql("SELECT COUNT(*) FROM account_namecards;"));
        }

        [TestCase("straight-flush")]
        [TestCase("royal-straight-flush")]
        public void CatalogHasBothArtworkVariantsAndCannotExportPurchasableRewards(string id)
        {
            var product = Resources.Load<NamecardCatalogSO>(NamecardCatalogSO.ResourcePath).GetProduct(id);
            Assert.IsTrue(product.IsListed);
            Assert.IsTrue(product.IsReward);
            Assert.IsFalse(product.CanPurchase);
            Assert.IsFalse(_catalog.Products.Single(p => p.Id == id).Listed);
            Assert.IsEmpty(Game.Editor.NamecardArtValidation.GetIssues(product));
            Assert.AreSame(product.Artwork, product.GetArtwork(8f / 3));
            Assert.AreSame(product.Artwork4x3, product.GetArtwork(4f / 3));
            Assert.IsFalse(product.Description.Contains("领取"));
            Assert.IsFalse(product.Description.Contains("获得"));
        }

        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        [TestCase("straight-flush")]
        [TestCase("royal-straight-flush")]
        [TestCase("devil-service")]
        public void EveryNonPurchasableCardCanBeEquippedWhenOwned(string id)
        {
            var holder = new GameObject("Reward namecard UI test");
            holder.SetActive(false);
            try
            {
                var panel = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/Panels/ShopPanel/ShopPanel.prefab"), holder.transform).GetComponent<ShopPanel>();
                var product = panel.Catalog.GetProduct(id);
                var card = Field<ShopItemCard[]>(panel, "_cards")[0];
                string selected = null;
                panel.EquipRequested += value => selected = value;
                panel.SetAccountView("LongPlayerName123", 0, new[] { "default" }, "", true);
                card.Present(product, "LongPlayerName123", 0, true, false, false, true, null);
                Assert.AreEqual("不可购买", Field<TMP_Text>(card, "_actionLabel").text);
                Assert.IsFalse(Field<Button>(card, "_action").interactable);
                panel.SetAccountView("LongPlayerName123", 0, new[] { "default", id }, "", true);
                card.Present(product, "LongPlayerName123", 0, true, true, false, true,
                    p => typeof(ShopPanel).GetMethod("SelectProduct", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, new object[] { p }));
                // The tab is serialized as Items; select the real Namecards tab before acting.
                panel.SelectTab(ShopTab.Namecards);
                card.Present(product, "LongPlayerName123", 0, true, true, false, true,
                    p => typeof(ShopPanel).GetMethod("SelectProduct", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, new object[] { p }));
                Assert.AreEqual("装备", Field<TMP_Text>(card, "_actionLabel").text);
                Assert.IsTrue(Field<Button>(card, "_action").interactable);
                typeof(ShopItemCard).GetMethod("Act", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(card, null);
                Assert.AreEqual(id, selected);
                card.Present(product, "LongPlayerName123", 0, true, true, true, true, null);
                Assert.AreEqual("已装备", Field<TMP_Text>(card, "_actionLabel").text);
                Assert.IsFalse(Field<Button>(card, "_action").interactable);
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }
    }
}
