using System;
using System.Collections.Generic;
using System.Linq;
using Game.Achievements;
using Game.Network.Accounts;

namespace Game.Network
{
    public partial class GameManager
    {
        private sealed class EndedHand
        {
            public RoundResult Result;
            public string[] Participants;
            public readonly HashSet<string> Confirmed = new HashSet<string>(StringComparer.Ordinal);
            public string WinnerAccountId;
            public List<Poker> WinnerHole;
            public AchievementContext Context;
        }

        private readonly Dictionary<int, EndedHand> _endedHands = new Dictionary<int, EndedHand>();
        public event Action<int, RoundResult> OnRoundResultRevealed;

        private void RememberEndedHand(ServerGameInfo info, RoundResult result)
        {
            foreach (var player in result.PlayerResults)
                PopulatePublicResultPattern(result.Community, player);
            var winner = info.Players.FirstOrDefault(p => p.ParticipatedThisHand && !p.IsFold);
            _endedHands[info.RoomInfo.Id] = new EndedHand
            {
                Result = result.ForSpectator(),
                Participants = info.Players.Where(p => p.ParticipatedThisHand).Select(p => p.PlayerName).ToArray(),
                WinnerAccountId = result.RevealPlayerName == null ? null : winner?.AccountId,
                WinnerHole = result.RevealPlayerName == null ? null : new List<Poker>(winner.Hole),
                Context = AchievementHistoryFor(info.HandId).Latest,
            };
        }

        private bool IsResultParticipantEligible(int roomId, string name, Func<string, bool> connected)
        {
            var info = GetGameInfo(roomId);
            var seat = info?.RoomInfo.FindSeat(name);
            if (seat == null || seat.LeaveAfterHand || seat.SitOutAfterHand ||
                seat.State == RoomSeatState.Watching || seat.State == RoomSeatState.Leaving || !connected(name))
                return false;
            var player = info.Players.FirstOrDefault(p => p.IsOccupied && p.PlayerName == name);
            return player != null && player.Chips >= GetBigBlindAmount(info);
        }

        public RoundReadySync GetRoundReady(int roomId, string name, Func<string, bool> connected)
        {
            var state = new RoundReadySync { RoomId = roomId };
            if (!_endedHands.TryGetValue(roomId, out var ended) ||
                GetGameInfo(roomId)?.Stage != GameStage.RoundOver)
                return state;
            var eligible = ended.Participants.Where(p => IsResultParticipantEligible(roomId, p, connected)).ToArray();
            state.HandId = ended.Result.HandId;
            state.RequiredCount = eligible.Length;
            state.ConfirmedCount = eligible.Count(ended.Confirmed.Contains);
            state.RequiresConfirmation = eligible.Contains(name);
            state.IsConfirmed = state.RequiresConfirmation && ended.Confirmed.Contains(name);
            state.CanReveal = !string.IsNullOrEmpty(name) && !ended.Result.IsRevealed && ended.Result.RevealPlayerName == name && connected(name) &&
                GetGameInfo(roomId).RoomInfo.ContainsMember(name);
            return state;
        }

        public bool ConfirmRound(int roomId, string handId, string name, Func<string, bool> connected)
        {
            var state = GetRoundReady(roomId, name, connected);
            if (string.IsNullOrEmpty(handId) || state.HandId != handId || !state.RequiresConfirmation)
                return false;
            _endedHands[roomId].Confirmed.Add(name);
            return true;
        }

        public bool AreRoundParticipantsReady(int roomId, string handId, Func<string, bool> connected)
        {
            var state = GetRoundReady(roomId, null, connected);
            return !string.IsNullOrEmpty(handId) && state.HandId == handId && state.RequiredCount == state.ConfirmedCount;
        }

        public bool RevealLastHand(int roomId, string handId, string name, Func<string, bool> connected)
        {
            var state = GetRoundReady(roomId, name, connected);
            if (string.IsNullOrEmpty(handId) || state.HandId != handId || !state.CanReveal)
                return false;
            var ended = _endedHands[roomId];
            // Persist only reveal-specific facts; never re-run settlement or winner events.
            PublishRevealedHandFacts(roomId, ended.Context, ended.WinnerAccountId, ended.WinnerHole,
                ended.Result.Community, handId);
            var player = ended.Result.PlayerResults.Single(p => p.PlayerName == name);
            player.Hole = new List<Poker>(ended.WinnerHole);
            PopulatePublicResultPattern(ended.Result.Community, player);
            ended.Result.IsRevealed = true;
            OnRoundResultRevealed?.Invoke(roomId, ended.Result.ForSpectator());
            return true;
        }

        private static void PopulatePublicResultPattern(List<Poker> community, RoundResult.Player player)
        {
            if (player.IsFold || player.Hole.Count != 2 || community.Count < 3) return;
            var cards = community.Concat(player.Hole).ToList();
            PokerPattern? best = null;
            for (int a = 0; a < cards.Count - 4; a++)
            for (int b = a + 1; b < cards.Count - 3; b++)
            for (int c = b + 1; c < cards.Count - 2; c++)
            for (int d = c + 1; d < cards.Count - 1; d++)
            for (int e = d + 1; e < cards.Count; e++)
            {
                var hand = new List<Poker> { cards[a], cards[b], cards[c], cards[d], cards[e] };
                var pattern = PokerHand.Create(hand).GetPattern();
                if (best.HasValue && !pattern.BiggerThan(best.Value)) continue;
                best = pattern;
                player.BestRank = pattern.Rank;
                player.BestGrades = new List<int>(pattern.Grades);
                player.BestHand = hand;
            }
        }

        public void RefreshWalletBalance(int roomId, PlayerAccount account)
        {
            var info = GetGameInfo(roomId);
            var player = info?.Players.FirstOrDefault(p => p.IsOccupied && p.AccountId == account.AccountId);
            if (player == null) return;
            player.AccountChips = account.Chips;
            if (!IsHandInProgress(roomId) || !player.ParticipatedThisHand)
                player.Chips = account.Chips;
            else if (player.IsFold)
                player.Chips = checked(account.Chips - player.RoundBet);
        }
    }
}
