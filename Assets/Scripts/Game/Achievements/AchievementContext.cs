using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network;

namespace Game.Achievements
{
    // Immutable, server-only snapshots. Never add these types to a client message.
    public sealed class AchievementPlayer
    {
        public string AccountId { get; }
        public string Name { get; }
        public int Seat { get; }
        public bool Folded { get; }
        public bool AllIn { get; }
        public bool Disconnected { get; }
        public uint StartingChips { get; }
        public uint Chips { get; }
        public uint StreetBet { get; }
        public uint Contribution { get; }
        public IReadOnlyList<Poker> Hole { get; }
        public PokerHandRank? BestRank { get; }
        public IReadOnlyList<int> Grades { get; }
        public uint Award { get; }
        public uint Refund { get; }

        public AchievementPlayer(string accountId, string name, int seat, bool folded, bool allIn, bool disconnected,
            uint startingChips, uint chips, uint streetBet, uint contribution, IReadOnlyList<Poker> hole,
            PokerHandRank? bestRank, IReadOnlyList<int> grades, uint award, uint refund)
        {
            AccountId = accountId; Name = name; Seat = seat; Folded = folded; AllIn = allIn; Disconnected = disconnected;
            StartingChips = startingChips; Chips = chips; StreetBet = streetBet; Contribution = contribution;
            Hole = Array.AsReadOnly((hole ?? Array.Empty<Poker>()).ToArray());
            BestRank = bestRank; Grades = Array.AsReadOnly((grades ?? Array.Empty<int>()).ToArray());
            Award = award; Refund = refund;
        }

        public bool Beats(AchievementPlayer other)
        {
            if (!BestRank.HasValue || !other.BestRank.HasValue) return false;
            if (BestRank != other.BestRank) return BestRank.Value > other.BestRank.Value;
            for (var i = 0; i < Math.Min(Grades.Count, other.Grades.Count); i++)
                if (Grades[i] != other.Grades[i]) return Grades[i] > other.Grades[i];
            return false;
        }
    }

    public sealed class AchievementAction
    {
        public string AccountId { get; }
        public GameStage Stage { get; }
        public ulong TurnId { get; }
        public PlayerOperationType Type { get; }
        public uint StreetBet { get; }
        public uint RemainingChips { get; }
        public bool Disconnected { get; }

        public AchievementAction(string accountId, GameStage stage, ulong turnId, PlayerOperationType type,
            uint streetBet, uint remainingChips, bool disconnected)
        {
            AccountId = accountId; Stage = stage; TurnId = turnId; Type = type;
            StreetBet = streetBet; RemainingChips = remainingChips; Disconnected = disconnected;
        }
    }

    public sealed class AchievementStreet
    {
        public GameStage Stage { get; }
        public uint Pot { get; }
        public IReadOnlyList<Poker> Community { get; }
        public IReadOnlyList<AchievementPlayer> Players { get; }

        public AchievementStreet(GameStage stage, uint pot, IReadOnlyList<Poker> community, IReadOnlyList<AchievementPlayer> players)
        {
            Stage = stage; Pot = pot;
            Community = Array.AsReadOnly(community.ToArray());
            Players = Array.AsReadOnly(players.ToArray());
        }
    }

    public sealed class AchievementPot
    {
        public uint Chips { get; }
        public IReadOnlyList<string> Eligible { get; }
        public IReadOnlyList<string> Winners { get; }

        public AchievementPot(uint chips, IReadOnlyList<string> eligible, IReadOnlyList<string> winners)
        {
            Chips = chips; Eligible = Array.AsReadOnly(eligible.ToArray()); Winners = Array.AsReadOnly(winners.ToArray());
        }
    }

    public sealed class AchievementContext
    {
        public string EventId { get; }
        public string HandId { get; }
        public int RoomId { get; }
        public GameStage? PreviousStage { get; }
        public GameStage Stage { get; }
        public int Dealer { get; }
        public int SmallBlind { get; }
        public int BigBlind { get; }
        public uint SmallBlindChips { get; }
        public bool Showdown { get; }
        public long OccurredAt { get; }
        public IReadOnlyList<AchievementStreet> Streets { get; }
        public IReadOnlyList<AchievementAction> Actions { get; }
        public IReadOnlyList<AchievementPot> Pots { get; }
        public IReadOnlyList<AchievementPlayer> Players => Streets[Streets.Count - 1].Players;

        public AchievementContext(string eventId, string handId, int roomId, GameStage? previousStage, GameStage stage,
            int dealer, int smallBlind, int bigBlind, uint smallBlindChips, bool showdown, long occurredAt,
            IReadOnlyList<AchievementStreet> streets, IReadOnlyList<AchievementAction> actions, IReadOnlyList<AchievementPot> pots)
        {
            EventId = eventId; HandId = handId; RoomId = roomId; PreviousStage = previousStage; Stage = stage;
            Dealer = dealer; SmallBlind = smallBlind; BigBlind = bigBlind; SmallBlindChips = smallBlindChips;
            Showdown = showdown; OccurredAt = occurredAt;
            Streets = Array.AsReadOnly(streets.ToArray()); Actions = Array.AsReadOnly(actions.ToArray());
            Pots = Array.AsReadOnly(pots.ToArray());
        }
    }
}
