using System;
using System.Collections.Generic;
using System.Linq;
using Game.Facts;

namespace Game.Tasks
{
    public sealed class TaskContext
    {
        public bool IsWithinValidity { get; set; }
        public ulong PlayerChip { get; set; }
    }

    public sealed class HandUse
    {
        public string HandId { get; set; }
        public int Round { get; set; }
        public List<string> Objectives { get; set; } = new List<string>();
    }

    public sealed class TaskState
    {
        public int CompletedCount { get; set; }
        public int ClaimedCount { get; set; }
        public int MaxCompletions { get; set; } = 1;
        public int[] Progress { get; set; } = Array.Empty<int>();
        public List<HandUse> HandUses { get; set; } = new List<HandUse>();
        public bool CanAccumulate => CompletedCount == ClaimedCount && CompletedCount < MaxCompletions;
        public bool HasPendingReward => CompletedCount > ClaimedCount;
    }

    public interface IHandCondition
    {
        bool RequiresSettlement { get; }
        bool Matches(HandFact fact);
        string Description { get; }
    }

    public sealed class HandSettledCondition : IHandCondition
    {
        public bool RequiresSettlement => true;
        public bool Matches(HandFact fact) => fact.Kind == HandFactKind.Settled;
        public string Description => "正常完成手牌";
    }

    public sealed class WonCondition : IHandCondition
    {
        public bool RequiresSettlement => true;
        public bool Matches(HandFact fact) => fact.Kind == HandFactKind.Settled && fact.WonAnyPot;
        public string Description => "获胜（含平分奖池）";
    }

    public sealed class PatternObservedCondition : IHandCondition
    {
        public FormedPattern Pattern { get; }
        public bool RequiresSettlement => false;
        public PatternObservedCondition(FormedPattern pattern) { Pattern = pattern; }
        public bool Matches(HandFact fact) => (fact.Patterns & Pattern) == Pattern;
        public string Description => "曾组成" + PatternNames.Get(Pattern);
    }

    public static class PatternNames
    {
        public static string Get(FormedPattern pattern) => pattern switch
        {
            FormedPattern.Pair => "一对", FormedPattern.TwoPair => "两对",
            FormedPattern.ThreeOfAKind => "三条", FormedPattern.Straight => "顺子",
            FormedPattern.Flush => "同花", FormedPattern.FullHouse => "葫芦",
            FormedPattern.FourOfAKind => "四条", FormedPattern.StraightFlush => "同花顺", _ => "未知牌型"
        };
    }

    public sealed class ObjectivePlan
    {
        public string Id { get; }
        public int Target { get; }
        public IReadOnlyList<IHandCondition> Conditions { get; }
        public string Description => string.Join("且", Conditions.Select(c => c.Description)) + " " + Target + " 手";
        public ObjectivePlan(string id, int target, params IHandCondition[] conditions)
        {
            if (string.IsNullOrWhiteSpace(id) || target < 1 || target > 100000 || conditions == null || conditions.Length == 0)
                throw new ArgumentException("Invalid hand objective.");
            Id = id; Target = target; Conditions = Array.AsReadOnly(conditions.ToArray());
        }
        public bool Matches(HandFact fact)
        {
            var kind = Conditions.Any(c => c.RequiresSettlement) ? HandFactKind.Settled : HandFactKind.PatternObserved;
            // A voluntary reveal is a pattern observation only. It must never replay
            // a hand-count or victory objective, even when it follows a winning hand.
            if (fact.Kind == HandFactKind.CardsRevealed)
                return kind == HandFactKind.PatternObserved &&
                    Conditions.All(c => c is PatternObservedCondition && c.Matches(fact));
            return fact.Kind == kind && Conditions.All(c => c.Matches(fact));
        }
    }

    public abstract class GameTask
    {
        private readonly Reward _reward;
        protected GameTask(Reward reward) { _reward = reward; }
        public abstract bool IsDirectClaim { get; }
        public abstract bool Evaluate(HandFact fact, TaskState state);
        public abstract bool Available(TaskContext context, TaskState state);
        public Reward GetReward() => _reward;
        public virtual IReadOnlyList<ObjectivePlan> Objectives => Array.Empty<ObjectivePlan>();
        public virtual string Description => "直接领取奖励";
    }

    public class DirectClaim : GameTask
    {
        public DirectClaim(Reward reward) : base(reward) { }
        public override bool IsDirectClaim => true;
        public override bool Evaluate(HandFact fact, TaskState state) => false;
        public override bool Available(TaskContext context, TaskState state) =>
            context.IsWithinValidity && state.ClaimedCount < state.MaxCompletions;
    }

    public sealed class DirectClaimWhenLesserThan : DirectClaim
    {
        public uint Threshold { get; }
        public DirectClaimWhenLesserThan(Reward reward, uint threshold) : base(reward) { Threshold = threshold; }
        public override bool Available(TaskContext context, TaskState state) =>
            base.Available(context, state) && context.PlayerChip < Threshold;
        public override string Description => "领取时总资产低于 $" + Threshold.ToString("N0");
    }

    public sealed class DirectClaimWhenAtMost : DirectClaim
    {
        public uint Threshold { get; }
        public DirectClaimWhenAtMost(Reward reward, uint threshold) : base(reward) { Threshold = threshold; }
        public override bool Available(TaskContext context, TaskState state) =>
            base.Available(context, state) && context.PlayerChip <= Threshold;
        public override string Description => "领取时总资产不高于 $" + Threshold.ToString("N0");
    }

    public class ObjectiveTask : GameTask
    {
        private readonly IReadOnlyList<ObjectivePlan> _objectives;
        public ObjectiveTask(Reward reward, params ObjectivePlan[] objectives) : base(reward)
        {
            if (objectives == null || objectives.Length == 0 || objectives.Select(o => o.Id).Distinct().Count() != objectives.Length)
                throw new ArgumentException("Objectives must have unique IDs.");
            _objectives = Array.AsReadOnly(objectives.ToArray());
        }
        public override bool IsDirectClaim => false;
        public override IReadOnlyList<ObjectivePlan> Objectives => _objectives;
        public override string Description => string.Join("；", _objectives.Select(o => o.Description));
        public override bool Available(TaskContext context, TaskState state) => state.HasPendingReward;
        public override bool Evaluate(HandFact fact, TaskState state)
        {
            if (!state.CanAccumulate) return false;
            if (state.Progress.Length != _objectives.Count) throw new InvalidOperationException("Task progress shape changed.");
            var use = state.HandUses.Find(h => h.HandId == fact.HandId);
            if (use != null && use.Round != state.CompletedCount) return false;
            var changed = false;
            for (var index = 0; index < _objectives.Count; index++)
            {
                var objective = _objectives[index];
                if (state.Progress[index] >= objective.Target || !objective.Matches(fact) ||
                    (use != null && use.Objectives.Contains(objective.Id))) continue;
                if (use == null)
                {
                    use = new HandUse { HandId = fact.HandId, Round = state.CompletedCount };
                    state.HandUses.Add(use);
                }
                use.Objectives.Add(objective.Id);
                state.Progress[index]++;
                changed = true;
            }
            if (changed && _objectives.Select((o, i) => state.Progress[i] >= o.Target).All(v => v))
                state.CompletedCount++;
            return changed;
        }
    }

    public class CountHands : ObjectiveTask
    {
        public CountHands(Reward reward, int target, params IHandCondition[] conditions)
            : base(reward, new ObjectivePlan("hands", target, conditions)) { }
    }
    public sealed class PlayHands : CountHands
    {
        public PlayHands(Reward reward, int target) : base(reward, target, new HandSettledCondition()) { }
    }
    public sealed class FormPattern : CountHands
    {
        public FormPattern(Reward reward, FormedPattern pattern, int target)
            : base(reward, target, new PatternObservedCondition(pattern)) { }
    }
    public sealed class WinHands : CountHands
    {
        public WinHands(Reward reward, int target) : base(reward, target, new WonCondition(), new HandSettledCondition()) { }
    }
    public sealed class WinWithPattern : CountHands
    {
        public WinWithPattern(Reward reward, FormedPattern pattern, int target)
            : base(reward, target, new PatternObservedCondition(pattern), new WonCondition(), new HandSettledCondition()) { }
    }
    public sealed class AllObjectives : ObjectiveTask
    {
        public AllObjectives(Reward reward, params ObjectivePlan[] objectives) : base(reward, objectives) { }
    }
}
