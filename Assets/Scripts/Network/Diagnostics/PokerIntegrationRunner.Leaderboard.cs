using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Network.Accounts;
using Game.UI;
using Game.UI.GamePanelElement;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Network.Diagnostics
{
    public sealed partial class PokerIntegrationRunner
    {
        private bool _testLeaderboard;
        private bool _testIdle;

        private void SeedLeaderboard()
        {
#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
            var repository = (_authenticator.AccountRepository as GuardedAccountRepository).InnerRepository;
            for (int index = 0; index < 11; index++)
            {
                var account = new PlayerAccount
                {
                    AccountId = "offline-" + index.ToString("D2"), UserName = "Offline" + index.ToString("D2"),
                    Chips = 500000, PasswordAlgorithm = "IntegrationFixture", PasswordHash = new byte[] { 1 },
                    PasswordSalt = new byte[] { 2 }, PasswordIterations = 1,
                    CreatedAtUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAtUtc = DateTime.UtcNow,
                };
                if (repository.Create(account) != CreateAccountResult.Created)
                    throw new InvalidOperationException("Unable to seed isolated leaderboard test database.");
                ExecuteStorageTestSql("UPDATE player_accounts SET equipped_namecard='galaxy' WHERE account_id='offline-" + index.ToString("D2") + "';");
            }
#endif
        }

        private IEnumerator CheckLeaderboardUI()
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while ((!_authenticated || RoomListPanel.Instance == null || !RoomListPanel.Instance.IsShowing) &&
                Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!_authenticated || RoomListPanel.Instance == null || !RoomListPanel.Instance.IsShowing)
            {
                Fail("Authenticated lobby did not open before leaderboard check.");
                yield break;
            }
            Framework.UI.UIManager.Instance.ResetPanels<TitlePanel>();
            Click(TitlePanel.Instance, "_leaderboard");
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var panel = LeaderboardPanel.Instance;
                if (panel == null || !panel.IsShowing)
                {
                    Fail("Leaderboard entry did not open its registered panel.");
                    yield break;
                }
                deadline = Time.realtimeSinceStartup + 12f;
                while (Field<ulong>(panel, "_requestId") != 0 && Time.realtimeSinceStartup < deadline)
                    yield return null;
                var rows = Field<RectTransform>(panel, "_list").GetComponentsInChildren<LeaderboardRow>();
                var self = Field<LeaderboardRow>(panel, "_self");
                long rank;
                if (rows.Length != 10 || rows.Any(row => !Field<TMP_Text>(row, "_name").text.StartsWith("Offline")) ||
                    !long.TryParse(Field<TMP_Text>(self, "_rank").text, out rank) || rank < 12 || rank > 14 ||
                    Field<TMP_Text>(self, "_name").text != _userName + "（我）" ||
                    Field<TMP_Text>(self, "_chips").text != "$" + GameController.Instance.State.Chips.ToString("N0") ||
                    self.transform.IsChildOf(Field<ScrollRect>(panel, "_scroll").viewport))
                {
                    Fail("Leaderboard top ten / independent self row / account balance was incorrect: " + Field<TMP_Text>(panel, "_feedback").text);
                    yield break;
                }
                foreach (var row in rows)
                {
                    var appearance = Field<NamecardAppearance>(row, "_namecard");
                    var artwork = Field<Image>(appearance, "_artwork");
                    if (artwork.sprite != Resources.Load<Game.SO.NamecardCatalogSO>("SO/Shop/NamecardCatalog").Products.First(product => product.Id == "galaxy").Artwork ||
                        Mathf.Abs(((RectTransform)appearance.transform).rect.width / ((RectTransform)appearance.transform).rect.height - 8f / 3f) > 0.001f)
                    {
                        Fail("Leaderboard equipped namecard was lost in network serialization or rendered at the wrong aspect ratio.");
                        yield break;
                    }
                }
                if (attempt == 0)
                {
                    deadline = Time.realtimeSinceStartup + 3f;
                    while (!Field<Button>(panel, "_refresh").IsInteractable() && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Click(panel, "_refresh");
                }
            }
            Click(LeaderboardPanel.Instance, "_back");
            if (Framework.UI.UIManager.Instance.TopPanelIdentifier != nameof(TitlePanel))
            {
                Fail("Feature back did not return to title.");
                yield break;
            }
            Click(TitlePanel.Instance, "_start");
            WriteResult("leaderboard-" + _clientIndex + "-" + _roundsCompleted + ".checked", "Title entry and back, top ten offline accounts, equipped galaxy artwork at 8:3, own rank outside top ten, settled balance, manual refresh and back verified through actual network and UI.");
        }

        private IEnumerator CheckIdleConnection()
        {
            Framework.UI.UIManager.Instance.ResetPanels<TitlePanel>();
            var started = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - started < 310f)
            {
                if (!NetworkClient.isConnected || !NetworkClient.connection.isAuthenticated)
                {
                    Fail("Authenticated connection was kicked while idle.");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Framework.UI.UIManager.Instance.ResetPanels<RoomListPanel>();
            yield return CheckLeaderboardUI();
            if (File.Exists(ResultPath(_clientIndex, "fail"))) yield break;
            _finishing = true;
            LoginCredentialStore.Clear();
            _networkRoom.StopClient();
            WriteResult(ResultPath(_clientIndex, "pass"), "Authenticated idle connection survived 310 seconds without business requests; leaderboard still worked afterward.");
            Application.Quit(0);
        }

        private void InspectPublicResult(RoundResult result)
        {
            bool showdown = result.Community.Count == 5 && result.PlayerResults.Count(player => !player.IsFold) > 1;
            if (result.PlayerResults.Any(player => player.IsFold ? player.Hole.Count != 0 : showdown && player.Hole.Count != 2))
            {
                Fail("Result did not preserve public showdown cards or exposed folded cards.");
                return;
            }
            var panel = RoundResultMsgBox.Instance;
            if (panel == null || !panel.IsShowing)
            {
                Fail("Public result panel did not open.");
                return;
            }
            var rows = Field<List<RoundResultListItem>>(panel, "_listItems");
            foreach (var player in result.PlayerResults)
            {
                var row = rows.Single(item => item.PlayerName == player.PlayerName);
                if (Field<List<PokerElement>>(row, "_holes").Count(card => card.IsShowing) != player.Hole.Count ||
                    row.ShowPattern != (!player.IsFold && player.Hole.Count == 2 && result.Community.Count == 5))
                    Fail("Result UI did not render exactly the public cards and hand pattern.");
            }
            if (_latestGameInfo.HasValue && _latestGameInfo.Value.Info.IsSpectator)
                WriteResult("spectator-cards-" + _clientIndex + "-" + _roundsCompleted + ".checked", "Observer received and rendered publicly revealed cards; folded hands remained hidden.");
        }
    }
}
