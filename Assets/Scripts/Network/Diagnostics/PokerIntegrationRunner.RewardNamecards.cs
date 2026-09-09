using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Framework.UI;
using Game.Achievements;
using Game.Network.Accounts;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Network.Diagnostics
{
    public sealed partial class PokerIntegrationRunner
    {
        private bool _testRewardNamecards;

        private void ConfigureRewardNamecardFixture()
        {
            var seeded = false;
            Field<GameManager>(_networkRoom, "_gameManager").OnHandPrepared += info =>
            {
                if (seeded) return;
                seeded = true;
                info.Players.First(p => p.PlayerName.EndsWith("_0", StringComparison.Ordinal)).Hole = new List<Poker> {
                    new Poker(PokerSuit.Spade, PokerRank.Ace), new Poker(PokerSuit.Spade, PokerRank.King) };
                info.Players.First(p => p.PlayerName.EndsWith("_1", StringComparison.Ordinal)).Hole = new List<Poker> {
                    new Poker(PokerSuit.Heart, PokerRank.Two), new Poker(PokerSuit.Club, PokerRank.Three) };
                info.Deck = new Queue<Poker>(new[] {
                    new Poker(PokerSuit.Heart, PokerRank.Four), new Poker(PokerSuit.Spade, PokerRank.Ten),
                    new Poker(PokerSuit.Spade, PokerRank.Jack), new Poker(PokerSuit.Spade, PokerRank.Queen),
                    new Poker(PokerSuit.Heart, PokerRank.Five), new Poker(PokerSuit.Diamond, PokerRank.Seven),
                    new Poker(PokerSuit.Heart, PokerRank.Six), new Poker(PokerSuit.Club, PokerRank.Eight) });
            };
        }

        private static ShopItemCard FindRewardCard(ShopPanel panel, string id)
        {
            var products = Field<List<Game.SO.NamecardProductSO>>(panel, "_products");
            var index = products.FindIndex(p => p.Id == id);
            if (index < 0) throw new InvalidOperationException("Reward namecard is missing from the shop: " + id);
            panel.GoToPage(index / ShopPanel.PageSize);
            return Field<ShopItemCard[]>(panel, "_cards")[index % ShopPanel.PageSize];
        }

        private IEnumerator CheckRewardNamecardsUI()
        {
            var ids = new[] { "straight-flush", "royal-straight-flush" };
            var achievements = new[] { "showdown_straight_flush", "royal_straight_flush" };
            UIManager.Instance.ResetPanels<TitlePanel>();
            Click(TitlePanel.Instance, "_shop");
            var shop = ShopPanel.Instance;
            shop.SelectTab(ShopTab.Namecards);
            yield return ShopWait(shop);
            foreach (var id in ids)
            {
                var card = FindRewardCard(shop, id);
                if (Field<Button>(card, "_action").interactable || Field<TMP_Text>(card, "_actionLabel").text != "不可购买")
                { Fail("Unowned reward namecard is purchasable: " + id); yield break; }
                // Bypass the disabled button to exercise authoritative rejection of a forged purchase.
                var before = GameController.Instance.State.Chips;
                typeof(ShopPanel).GetMethod("StartRequest", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(shop, new object[] { ShopAction.Purchase, id });
                yield return ShopWait(shop);
                if (GameController.Instance.State.Chips != before || Field<HashSet<string>>(shop, "_owned").Contains(id) ||
                    Field<TMP_Text>(shop, "_feedback").text != "这张名片已不可购买，请刷新商城。")
                { Fail("Server accepted a reward namecard purchase."); yield break; }
            }
            Click(shop, "_back");
            Click(TitlePanel.Instance, "_achievements");
            yield return WaitAchievementPanel();
            var panel = AchievementPanel.Instance;
            foreach (var id in achievements)
            {
                var view = Field<AchievementView[]>(panel, "_views").Single(v => v.Id == id);
                if ((view.CompletedAt > 0) != (_clientIndex == 0))
                { Fail("Reward achievement leaked to an opponent or spectator."); yield break; }
                if (_clientIndex != 0) continue;
                var before = GameController.Instance.State.Chips;
                var row = Field<List<AchievementRow>>(panel, "_rows").Single(r => Field<TMP_Text>(r, "_name").text == view.Name);
                if (!Field<TMP_Text>(row, "_reward").text.Contains("名片"))
                { Fail("Achievement UI omitted the reward namecard."); yield break; }
                Click(row, "_claim");
                yield return WaitAchievementPanel();
                if (GameController.Instance.State.Chips != before + view.RewardChips)
                { Fail("Reward namecard claim credited incorrect chips."); yield break; }
                _networkRoom.RequestAchievementClaim(id);
                yield return new WaitForSecondsRealtime(0.3f);
                if (GameController.Instance.State.Chips != before + view.RewardChips)
                { Fail("Repeated claim duplicated the reward."); yield break; }
            }
            UIManager.Instance.ResetPanels<TitlePanel>();
            Click(TitlePanel.Instance, "_shop");
            shop = ShopPanel.Instance;
            shop.SelectTab(ShopTab.Namecards);
            yield return ShopWait(shop);
            foreach (var id in ids)
            {
                var card = FindRewardCard(shop, id);
                if (Field<HashSet<string>>(shop, "_owned").Contains(id) != (_clientIndex == 0))
                { Fail("Claim did not update the correct cosmetic inventory."); yield break; }
                if (_clientIndex != 0) continue;
                if (Field<TMP_Text>(card, "_actionLabel").text != "装备")
                { Fail("Owned reward cannot be equipped."); yield break; }
                Click(card, "_action");
                yield return ShopWait(shop);
                if (Field<string>(shop, "_equippedId") != id)
                { Fail("Reward equip did not persist."); yield break; }
            }
            if (_clientIndex == 0)
            {
                _networkRoom.StopClient();
                _authenticated = false;
                yield return new WaitForSecondsRealtime(0.2f);
                _networkRoom.StartConfiguredClientConnection();
                RegisterClientHandlers();
                _authenticator.Login(_userName, Password);
                var deadline = Time.realtimeSinceStartup + 15;
                while (!_authenticated && Time.realtimeSinceStartup < deadline) yield return null;
                if (!_authenticated) { Fail("Reward namecard reconnect failed."); yield break; }
                UIManager.Instance.ResetPanels<TitlePanel>();
                Click(TitlePanel.Instance, "_shop");
                shop = ShopPanel.Instance;
                shop.SelectTab(ShopTab.Namecards);
                yield return ShopWait(shop);
                if (Field<string>(shop, "_equippedId") != ids[1] || ids.Any(id => !Field<HashSet<string>>(shop, "_owned").Contains(id)))
                { Fail("Reconnect lost reward ownership or equipment."); yield break; }
            }
            WriteResult("reward-namecards-" + _clientIndex + ".checked",
                "Real royal showdown, two achievement rewards, rejected forged purchases, account privacy, equip and reconnect verified.");
        }
    }
}
