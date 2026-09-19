using System;
using System.Collections.Generic;
using System.Linq;
using Game.Achievements;
using Game.Events;
using Game.Network.Accounts;

namespace Game.Network
{
    public partial class GameManager
    {
        private sealed class AchievementHistory
        {
            public readonly List<AchievementStreet> Streets = new List<AchievementStreet>();
            public readonly List<AchievementAction> Actions = new List<AchievementAction>();
            public AchievementContext Latest;
        }

        private readonly Dictionary<string, AchievementHistory> _achievementHistory = new Dictionary<string, AchievementHistory>();

        private AchievementHistory AchievementHistoryFor(string handId)
        {
            if (!_achievementHistory.TryGetValue(handId, out var history))
                _achievementHistory.Add(handId, history = new AchievementHistory());
            return history;
        }

        private void RecordAchievementAction(ServerGameInfo info)
        {
            if (string.IsNullOrEmpty(info.HandId) || info.OperatingPlayer < 0) return;
            var player = info.Players[info.OperatingPlayer];
            var action = player.LastOperation;
            if (action == null) return;
            var recorded = new AchievementAction(player.AccountId, info.Stage,
                action.TurnId, action.Type, player.StageBet, player.Chips, player.IsDisconnected,
                player.LastActionStackBefore, player.LastActionCommittedChips);
            AchievementHistoryFor(info.HandId).Actions.Add(recorded);
            if (_accountRepository is IPlayerEventRepository publisher)
            {
                publisher.PublishPlayerEvent(TableEventSource.Action(info.HandId, player.Hole, recorded,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            }
        }

        private void PublishAchievementStreet(ServerGameInfo info)
        {
            if (!(_accountRepository is IAchievementRepository publisher)) return;
            var history = AchievementHistoryFor(info.HandId);
            if (history.Latest?.Stage == info.Stage) return;
            publisher.PublishAchievementEvent(CaptureAchievementContext(info, info.Stage));
        }

        private AchievementContext CaptureAchievementContext(ServerGameInfo info, GameStage stage,
            bool showdown = false, IReadOnlyList<AchievementPot> pots = null,
            IReadOnlyList<uint> awards = null, IReadOnlyList<uint> refunds = null)
        {
            var history = AchievementHistoryFor(info.HandId);
            if (history.Latest?.Stage == stage) return history.Latest;
            var players = info.Players.Where(p => p.ParticipatedThisHand).Select(p =>
            {
                PokerPattern? pattern = null;
                if (!p.IsFold && info.Community.Count >= 3 && p.Hole.Count == 2)
                    pattern = AchievementPatternEvaluator.GetBest(info.Community.Concat(p.Hole).ToArray());
                var award = awards?[p.SeatIndex] ?? 0;
                var refund = refunds?[p.SeatIndex] ?? 0;
                return new AchievementPlayer(p.AccountId, p.PlayerName, p.SeatIndex, p.IsFold, p.IsAllIn, p.IsDisconnected,
                    p.HandStartingChips, checked(p.HandStartingChips - p.RoundBet + award + refund), p.StageBet, p.RoundBet, p.Hole,
                    pattern?.Rank, pattern?.Grades, award, refund);
            }).ToArray();
            var previous = history.Latest?.Stage;
            history.Streets.Add(new AchievementStreet(stage, info.TotalPot, info.Community, players));
            history.Latest = new AchievementContext(info.HandId + ":achievement:" + (int)stage, info.HandId, info.RoomInfo.Id,
                previous, stage, info.Dealer, info.SmallBlind, info.BigBlind, (uint)info.RoomInfo.SmallBlindChips,
                showdown, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), history.Streets, history.Actions,
                pots ?? Array.Empty<AchievementPot>());
            return history.Latest;
        }
    }
}
