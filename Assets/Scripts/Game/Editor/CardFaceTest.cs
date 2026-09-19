using System;
using System.IO;
using System.Linq;
using Game.Network;
using Game.Network.Accounts;
using Game.SO;
using Game.UI;
using Microsoft.Data.Sqlite;
using Mirror;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CardFaceTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private PlayerAccount _account;
        private CardFaceServerCatalog _catalog;

        [SetUp]
        public void Setup()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerCardFace-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            var service = new AccountService(_repository, new PasswordHasher(iterations: 1000));
            service.Initialize();
            _account = service.Register("CardFaceUser", "password123").Account;
            _catalog = new CardFaceServerCatalog { Products = new[] {
                new CardFaceOffer { Id = "default", Price = 0 }, new CardFaceOffer { Id = "test-style", Price = 1000 } } };
        }

        [TearDown]
        public void Cleanup()
        {
            CardFaceAppearance.Equip(null);
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        private ShopResult Change(ShopAction action, string id = "test-style", string operation = null, string revision = null)
            => _repository.ChangeCardFace(_account.AccountId, operation ?? Guid.NewGuid().ToString("N"),
                action, id, revision ?? _catalog.Revision, _catalog);

        [Test]
        public void DefaultIsOwnedAndPurchaseAndEquipSurviveRestartWithoutAffectingNamecard()
        {
            CollectionAssert.AreEqual(new[] { "default", "minimal" }, _repository.ReadCardFaces(_account.AccountId).OwnedIds);
            Assert.AreEqual(ShopResult.AlreadyOwned, Change(ShopAction.Purchase, "default"));
            var request = Guid.NewGuid().ToString("N");
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Purchase, operation: request));
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Purchase, operation: request));
            Assert.AreEqual(9000, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual("", _repository.FindById(_account.AccountId).EquippedCardFaceId);
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Equip));
            var reopened = new SqliteAccountRepository(_path);
            reopened.Initialize();
            Assert.AreEqual("test-style", reopened.FindById(_account.AccountId).EquippedCardFaceId);
            Assert.AreEqual("", reopened.FindById(_account.AccountId).EquippedNamecardId);
            CollectionAssert.AreEqual(new[] { "default" }, reopened.ReadNamecards(_account.AccountId).OwnedIds);
            Assert.IsTrue(reopened.ReconcileChips().IsConsistent);
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Equip, "default"));
            Assert.AreEqual("", reopened.FindById(_account.AccountId).EquippedCardFaceId);
        }

        [Test]
        public void ServerRejectsUnownedStaleQuoteRoomAndReusedOperationWithDifferentPayload()
        {
            Assert.AreEqual(ShopResult.NotOwned, Change(ShopAction.Equip));
            Assert.AreEqual(ShopResult.CatalogChanged, Change(ShopAction.Purchase, revision: "old"));
            _repository.SetLastRoom(_account.AccountId, 7);
            Assert.AreEqual(ShopResult.OutsideRoomRequired, Change(ShopAction.Purchase));
            _repository.SetLastRoom(_account.AccountId, null);
            var operation = Guid.NewGuid().ToString("N");
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Purchase, operation: operation));
            Assert.AreEqual(ShopResult.InvalidRequest, Change(ShopAction.Equip, operation: operation));
            Assert.AreEqual(9000, _repository.FindById(_account.AccountId).Chips);
        }

        [Test]
        public void FailedReceiptRollsBackDebitAndOwnership()
        {
            using (var connection = new SqliteConnection("Data Source=" + _path))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TRIGGER face_failure BEFORE INSERT ON card_face_requests BEGIN SELECT RAISE(ABORT,'Injected'); END;";
                command.ExecuteNonQuery();
            }
            Assert.Throws<SqliteException>(() => Change(ShopAction.Purchase));
            Assert.AreEqual(10000, _repository.FindById(_account.AccountId).Chips);
            CollectionAssert.AreEqual(new[] { "default", "minimal" }, _repository.ReadCardFaces(_account.AccountId).OwnedIds);
        }

        [Test]
        public void CardFaceItemIdGrantsShareShopOwnershipAndReceiptWithoutAutoEquipping()
        {
            var namecards = new NamecardServerCatalog { Products = new[] { new NamecardOffer { Id = "default" } } };
            var identities = new ItemIdentityRegistry(namecards, new ItemServerCatalog(), _catalog);
            var itemId = CardFaceItemId.ToItemId("test-style");
            Assert.AreEqual("cardface-test-style", itemId);
            Assert.AreEqual("test-style", CardFaceItemId.ToCardFaceId(itemId));
            Assert.IsTrue(identities.TryGet(itemId, out var identity));
            Assert.IsTrue(identity.IsCardFace && identity.IsEquippable);
            Assert.IsFalse(identity.ShowInInventory);
            var operation = Guid.NewGuid().ToString("N");
            var result = _repository.GrantItem(_account.AccountId, itemId, operation, 1, identities);
            Assert.AreEqual(ItemGrantResult.Success, result.Result);
            Assert.IsTrue(result.IsNewlyApplied);
            Assert.IsFalse(_repository.GrantItem(_account.AccountId, itemId, operation, 1, identities).IsNewlyApplied);
            Assert.AreEqual(ItemGrantResult.InvalidRequest, _repository.GrantItem(_account.AccountId, itemId, operation, 2, identities).Result);
            Assert.AreEqual(ItemGrantResult.AlreadyOwned, _repository.GrantItem(_account.AccountId, "cardface-default", Guid.NewGuid().ToString("N"), 1, identities).Result);
            Assert.AreEqual(10000, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual("", _repository.FindById(_account.AccountId).EquippedCardFaceId);
            Assert.IsEmpty(_repository.ReadItems(_account.AccountId).Items);
            Assert.AreEqual(ShopResult.AlreadyOwned, Change(ShopAction.Purchase));
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Equip));
            Assert.AreEqual("test-style", new SqliteAccountRepository(_path).FindById(_account.AccountId).EquippedCardFaceId);
        }

        [Test]
        public void AuthenticationAndShopProtocolCarryPrivateEquippedFace()
        {
            using var writer = NetworkWriterPool.Get();
            writer.Write(new AccountAuthenticationResponse { AccountId = "self", EquippedCardFaceId = "test-style" });
            writer.Write(new CardFaceShopResponse { AccountId = "self", EquippedId = "test-style", Offers = new[] {
                new CardFaceOfferMessage { Id = "test-style", Price = 1000, Listed = true } }, OwnedIds = new[] { "default", "test-style" } });
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            Assert.AreEqual("test-style", reader.Read<AccountAuthenticationResponse>().EquippedCardFaceId);
            var response = reader.Read<CardFaceShopResponse>();
            Assert.AreEqual("test-style", response.EquippedId);
            Assert.AreEqual(1000, response.Offers.Single().Price);
            Assert.IsFalse(System.Text.Json.JsonSerializer.Serialize(RoomInfo.CreateRoom("Owner")).Contains("CardFace"));
        }

        [Test]
        public void ClassicAssetsHaveAllCardsAndHiddenPokerUsesSelectedBack()
        {
            Game.Editor.CardFaceEditor.Validate();
            var catalog = Resources.Load<CardFaceCatalogSO>(CardFaceCatalogSO.ResourcePath);
            Assert.IsNotNull(catalog.Find("minimal"));
            CardFaceAppearance.Equip("missing-client-art");
            Assert.AreSame(catalog.Default, CardFaceAppearance.Current);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/GamePanel/Poker.prefab");
            var root = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var slot = root.GetComponent<PokerElement>();
                slot.Poker = new Poker(PokerSuit.Heart, PokerRank.King);
                slot.IsShowing = false;
                var view = root.GetComponentInChildren<CardFaceView>();
                Assert.IsNotNull(view);
                Assert.AreSame(catalog.Default.Cards.Back, view.Image.sprite);
                slot.Poker = new Poker(PokerSuit.Spade, PokerRank.Ace);
                Assert.AreSame(catalog.Default.Cards.Back, view.Image.sprite);
                slot.IsShowing = true;
                Assert.AreSame(catalog.Default.Cards.GetSprite(slot.Poker), view.Image.sprite);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
