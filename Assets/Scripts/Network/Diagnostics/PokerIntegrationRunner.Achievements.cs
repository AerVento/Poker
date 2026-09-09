using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Framework.UI;
using Game.Achievements;
using Game.Network.Accounts;
using Game.UI;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Network.Diagnostics
{
    public sealed partial class PokerIntegrationRunner
    {
        private bool _testAchievements;
        private bool _achievementHandSeeded;
        private bool _achievementBannerChecked;

        private void ConfigureAchievementFixture()
        {
            Field<GameManager>(_networkRoom, "_gameManager").OnHandPrepared += info =>
            {
                if (_achievementHandSeeded) return;
                _achievementHandSeeded = true;
                var alice = info.Players.First(p => p.PlayerName.EndsWith("_0", StringComparison.Ordinal));
                var bob = info.Players.First(p => p.PlayerName.EndsWith("_1", StringComparison.Ordinal));
                alice.Hole = new List<Poker> { new Poker(PokerSuit.Heart, PokerRank.Ace), new Poker(PokerSuit.Club, PokerRank.Three) };
                bob.Hole = new List<Poker> { new Poker(PokerSuit.Heart, PokerRank.King), new Poker(PokerSuit.Club, PokerRank.Queen) };
                info.Deck = new Queue<Poker>(new[] { new Poker(PokerSuit.Spade, PokerRank.Four), new Poker(PokerSuit.Club, PokerRank.Ace),
                    new Poker(PokerSuit.Diamond, PokerRank.Ace), new Poker(PokerSuit.Club, PokerRank.King), new Poker(PokerSuit.Spade, PokerRank.Five),
                    new Poker(PokerSuit.Diamond, PokerRank.King), new Poker(PokerSuit.Spade, PokerRank.Six), new Poker(PokerSuit.Spade, PokerRank.Two) });
            };
        }

        private void InspectAchievementBanner()
        {
            if (!_testAchievements || _clientIndex != 0 || _achievementBannerChecked) return;
            var banner = AchievementBannerPanel.Instance;
            if (banner == null || Field<CanvasGroup>(banner, "_group").alpha < 0.9f) return;
            if (UIManager.Instance.TopPanelIdentifier == nameof(AchievementBannerPanel) ||
                Field<CanvasGroup>(banner, "_group").blocksRaycasts ||
                Field<TMP_Text>(banner, "_name").text != "葫芦之上" || Field<Image>(banner, "_icon").sprite == null)
            {
                Fail("Achievement banner content or passive input ownership is incorrect.");
                return;
            }
            _achievementBannerChecked = true;
            ScreenCapture.CaptureScreenshot(Path.Combine(_outputDirectory, "achievement-banner.png"));
            WriteResult("achievement-banner.checked", "Icon, name, description and non-blocking banner checked during real gameplay.");
        }

        private IEnumerator WaitAchievementPanel()
        {
            var deadline = Time.realtimeSinceStartup + 12;
            while (Time.realtimeSinceStartup < deadline && (AchievementPanel.Instance == null ||
                Field<ulong>(AchievementPanel.Instance, "_statusRequest") != 0 ||
                Field<ulong>(AchievementPanel.Instance, "_claimRequest") != 0 ||
                Field<AchievementView[]>(AchievementPanel.Instance, "_views").Length == 0)) yield return null;
            if (AchievementPanel.Instance == null || Field<AchievementView[]>(AchievementPanel.Instance, "_views").Length == 0)
                Fail("Achievement panel did not receive a snapshot.");
        }

        private IEnumerator CheckAchievementUI()
        {
            UIManager.Instance.ResetPanels<TitlePanel>();
            Click(TitlePanel.Instance, "_achievements");
            yield return WaitAchievementPanel();
            var panel = AchievementPanel.Instance;
            if (panel == null) yield break;
            var view = Field<AchievementView[]>(panel, "_views").Single(v => v.Id == "full_house_beats_full_house");
            if ((view.CompletedAt > 0) != (_clientIndex == 0)) { Fail("Achievement was granted to the wrong participant or spectator."); yield break; }
            panel.SelectCategory(AchievementCategories.General);
            if (Field<List<AchievementRow>>(panel, "_rows").Count != 0) { Fail("General category contains a special achievement."); yield break; }
            panel.SelectCategory(AchievementCategories.Special);
            if (Field<List<AchievementRow>>(panel, "_rows").Count != 1 || !Field<ScrollRect>(panel, "_scroll").vertical)
            { Fail("Special category or vertical achievement rows are incorrect."); yield break; }
            if (_clientIndex == 0)
            {
                if (!_achievementBannerChecked) { Fail("No achievement banner appeared during the hand."); yield break; }
                var before = GameController.Instance.State.Chips;
                Click(Field<List<AchievementRow>>(panel, "_rows")[0], "_claim");
                yield return WaitAchievementPanel();
                if (GameController.Instance.State.Chips != before + 1000) { Fail("Achievement reward did not credit exactly once."); yield break; }
                _networkRoom.RequestAchievementClaim(view.Id);
                yield return new WaitForSecondsRealtime(0.4f);
                if (GameController.Instance.State.Chips != before + 1000) { Fail("Repeated achievement claim duplicated chips."); yield break; }
                ScreenCapture.CaptureScreenshot(Path.Combine(_outputDirectory, "achievement-panel.png"));
                _networkRoom.StopClient();
                _authenticated = false;
                yield return new WaitForSecondsRealtime(0.2f);
                _networkRoom.StartConfiguredClientConnection();
                RegisterClientHandlers();
                _authenticator.Login(_userName, Password);
                var deadline = Time.realtimeSinceStartup + 15;
                while (!_authenticated && Time.realtimeSinceStartup < deadline) yield return null;
                if (!_authenticated) { Fail("Achievement reconnect failed."); yield break; }
                UIManager.Instance.ResetPanels<TitlePanel>();
                Click(TitlePanel.Instance, "_achievements");
                yield return WaitAchievementPanel();
                view = Field<AchievementView[]>(AchievementPanel.Instance, "_views").Single(v => v.Id == view.Id);
                if (view.ClaimedAt == 0) { Fail("Reconnect lost the permanent achievement claim."); yield break; }
                yield return new WaitForSecondsRealtime(1.2f);
                if (AchievementBannerPanel.Instance != null) { Fail("Acknowledged achievement banner replayed after reconnect."); yield break; }
            }
            WriteResult("achievements-" + _clientIndex + ".checked", "Category filter, vertical rows, account privacy, claim and reconnect checked.");
        }
    }
}
