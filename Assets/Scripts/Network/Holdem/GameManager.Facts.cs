using System.Collections.Generic;
using System.Linq;
using Game.Facts;
using Game.Achievements;
using Game.Events;
using Game.Network.Accounts;

namespace Game.Network
{
    public partial class GameManager
    {
        private readonly Dictionary<string, FormedPattern> _formedPatterns = new Dictionary<string, FormedPattern>();
        private readonly HashSet<string> _preflopFacts = new HashSet<string>();

        private void PublishPreflopFacts(ServerGameInfo info)
        {
            if (info.Stage != GameStage.Preflop || _preflopFacts.Contains(info.HandId)) return;
            foreach (var player in info.Players.Where(p => p.ParticipatedThisHand && p.Hole.Count == 2))
                PublishPattern(info, player, FormablePatternEvaluator.Evaluate(player.Hole));
            _preflopFacts.Add(info.HandId);
        }

        private void PublishBoardFacts(ServerGameInfo info)
        {
            foreach (var player in info.Players.Where(p => p.ParticipatedThisHand && !p.IsFold))
                PublishPattern(info, player, FormablePatternEvaluator.Evaluate(player.Hole.Concat(info.Community)));
        }

        private void PublishPattern(ServerGameInfo info, ServerGameInfo.PlayerInfo player, FormedPattern observed)
        {
            var key = info.HandId + ":" + player.AccountId;
            _formedPatterns.TryGetValue(key, out var previous);
            var patterns = previous | observed;
            (_accountRepository as IHandFactPublisher)?.PublishHandFact(new HandFact
            {
                EventId = key + ":pattern:" + (int)info.Stage, AccountId = player.AccountId,
                HandId = info.HandId, Kind = HandFactKind.PatternObserved, Street = (int)info.Stage, Patterns = patterns
            });
            _formedPatterns[key] = patterns;
        }

        private HandFact CreateOutcomeFact(ServerGameInfo info, ServerGameInfo.PlayerInfo player, bool won)
        {
            _formedPatterns.TryGetValue(info.HandId + ":" + player.AccountId, out var patterns);
            return new HandFact
            {
                EventId = info.HandId + ":" + player.AccountId + ":settled", AccountId = player.AccountId,
                HandId = info.HandId, Kind = HandFactKind.Settled, Street = (int)info.Stage,
                Patterns = patterns, WonAnyPot = won
            };
        }

        private void PublishRevealedHandFacts(int roomId, AchievementContext context, string accountId,
            IReadOnlyList<Poker> hole, IReadOnlyList<Poker> community, string handId)
        {
            var occurredAt = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            (_accountRepository as IPlayerEventRepository)?.PublishPlayerEvent(
                TableEventSource.Revealed(handId, accountId, community, hole, occurredAt));
            (_accountRepository as IHandFactPublisher)?.PublishHandFact(new HandFact
            {
                EventId = handId + ":" + accountId + ":revealed",
                AccountId = accountId,
                HandId = handId,
                Kind = HandFactKind.CardsRevealed,
                Street = (int)(context?.PreviousStage ?? GameStage.Preflop),
                Patterns = FormablePatternEvaluator.Evaluate(hole.Concat(community)),
                WonAnyPot = false,
            });
        }

        private void ForgetHandFacts(string handId)
        {
            if (string.IsNullOrEmpty(handId)) return;
            _achievementHistory.Remove(handId);
            foreach (var key in _formedPatterns.Keys.Where(k => k.StartsWith(handId + ":", System.StringComparison.Ordinal)).ToArray())
                _formedPatterns.Remove(key);
            _preflopFacts.Remove(handId);
        }
    }
}
