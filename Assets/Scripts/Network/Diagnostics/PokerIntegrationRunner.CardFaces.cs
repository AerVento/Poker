using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Network.Accounts;
using Game.SO;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Network.Diagnostics
{
    public sealed partial class PokerIntegrationRunner
    {
        private const string FaceFixtureId = "integration-face";
        private readonly string _faceGrantOperation = Guid.NewGuid().ToString("N");
        private bool _faceGranted;

        private void ConfigureCardFaceFixture()
        {
            var catalog = Field<CardFaceServerCatalog>(_networkRoom, "_cardFaceShopCatalog");
            catalog.Products = catalog.Products.Concat(new[] { new CardFaceOffer { Id = FaceFixtureId, Price = 0 } }).ToArray();
            typeof(NetworkRoom).GetMethod("InitializeItemGrants", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance).Invoke(_networkRoom, null);
        }

        private void GrantCardFaceFixture()
        {
            if (_faceGranted) return;
            var name = $"Smoke{_runId.Substring(0, Math.Min(8, _runId.Length))}_1";
            var account = _authenticator.AccountRepository.FindByUserName(name);
            if (account == null) return;
            var result = _networkRoom.GrantItem(account.AccountId, CardFaceItemId.ToItemId(FaceFixtureId), _faceGrantOperation);
            if (result.Result != ItemGrantResult.Success) { Fail("Card-face reward grant failed."); return; }
            var retry = _networkRoom.GrantItem(account.AccountId, CardFaceItemId.ToItemId(FaceFixtureId), _faceGrantOperation);
            if (retry.IsNewlyApplied) { Fail("Card-face grant duplicated ownership."); return; }
            _faceGranted = true;
            WriteResult("card-face-grant.checked", "Server reward and idempotent retry succeeded.");
        }

        private IEnumerator CardFaceWait(CardFaceBrowserView view)
        {
            var until = Time.realtimeSinceStartup + 15;
            while ((Field<ulong>(_networkRoom, "_collectionRequest") != 0 || Field<CardFaceShopRequest>(view, "_pending").RequestId != 0) && Time.realtimeSinceStartup < until)
                yield return null;
            if (!Field<CardFaceShopResponse>(view, "_snapshot").HasSnapshot)
                Fail("Card-face shop did not return a snapshot.");
        }

        private IEnumerator CheckCardFaceUI()
        {
            var product = Instantiate(CardFaceAppearance.Catalog.Default);
            product.hideFlags = HideFlags.DontSave;
            typeof(CardFaceProductSO).GetField("_id", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance).SetValue(product, FaceFixtureId);
            Field<List<CardFaceProductSO>>(CardFaceAppearance.Catalog, "_products").Add(product);
            Click(TitlePanel.Instance, "_shop");
            var panel = ShopPanel.Instance;
            yield return ShopWait(panel);
            Click(panel, "_cardFacesTab");
            var view = panel.CardFacesView;
            yield return CardFaceWait(view);
            if (_finishing) yield break;
            while (view.transform.Find("CardFace-" + FaceFixtureId) == null)
            {
                var next = view.transform.Find("下一页")?.GetComponent<Button>();
                if (next == null || !next.interactable)
                { Fail("Card-face fixture was not found on any shop page."); yield break; }
                next.onClick.Invoke();
                yield return null;
            }
            var visibleCards = view.GetComponentsInChildren<CardFaceShopCard>();
            if (visibleCards.Length == 0 || visibleCards.Any(item => item.GetComponentsInChildren<CardFaceView>().Length != 2))
            { Fail("Each card-face product must preview exactly a front and a back."); yield break; }
            var card = view.transform.Find("CardFace-" + FaceFixtureId);
            var button = card.GetComponentInChildren<Button>();
            if (_clientIndex == 0)
            {
                button.onClick.Invoke();
                Click(Field<MsgBox>(view, "_confirmation"), "_cancelButton");
                if (Field<CardFaceShopRequest>(view, "_pending").RequestId != 0)
                { Fail("Cancel submitted a card-face purchase."); yield break; }
                button.onClick.Invoke();
                Click(Field<MsgBox>(view, "_confirmation"), "_confirmButton");
                yield return CardFaceWait(view);
            }
            else if (_clientIndex == 1)
            {
                if (!Field<CardFaceShopResponse>(view, "_snapshot").OwnedIds.Contains(FaceFixtureId))
                { Fail("Reward-granted face not shown as owned."); yield break; }
            }
            if (_clientIndex < 2)
            {
                view.transform.Find("CardFace-" + FaceFixtureId).GetComponentInChildren<Button>().onClick.Invoke();
                yield return CardFaceWait(view);
                if (CardFaceAppearance.EquippedId != FaceFixtureId)
                { Fail("Card face equipment was not applied locally."); yield break; }
            }
            Click(panel, "_back");
            Click(TitlePanel.Instance, "_shop");
            panel = ShopPanel.Instance;
            yield return ShopWait(panel);
            Click(panel, "_cardFacesTab");
            yield return CardFaceWait(panel.CardFacesView);
            if (CardFaceAppearance.EquippedId != (_clientIndex < 2 ? FaceFixtureId : "default"))
            { Fail("Reopened face shop lost account equipment."); yield break; }
            Click(panel, "_back");
            WriteResult("card-faces-" + _clientIndex + ".checked", "Real UI read, front/back preview, cancel, purchase/reward ownership, equip, reopen.");
        }

        private void CheckCardFaceGame()
        {
            if (CardFaceAppearance.EquippedId != (_clientIndex < 2 ? FaceFixtureId : "default"))
                Fail("Authentication/gameplay changed this player's private card face.");
            if (GamePanel.Instance == null) return;
            foreach (var poker in GamePanel.Instance.GetComponentsInChildren<PokerElement>())
                if (poker.GetComponentInChildren<CardFaceView>() == null)
                    Fail("A game card bypassed the selected visual prefab.");
        }
    }
}
