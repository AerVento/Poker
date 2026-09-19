using System;
using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    public abstract class NetWinningsAchievement : HandAchievement
    {
        public override string Category => AchievementCategories.Wealth;
        protected abstract bool Cumulative { get; }
        public override string IconPath => Cumulative ? "UI/Achievements/Wealth-v1" : "UI/Achievements/Champion-v1";

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver) return;
            var player = context.Players.FirstOrDefault(p => p.AccountId == accountId);
            if (player == null) return;
            // Use signed, wide arithmetic: losing hands and uncalled refunds never add winnings.
            var profit = (long)player.Award + player.Refund - player.Contribution;
            if (profit <= 0) return;
            // Event receipts already guarantee one application per account and settlement.
            var count = Cumulative ? (long)progress.Count + profit : Math.Max(progress.Count, profit);
            progress.Count = (int)Math.Min(Target, count);
        }
    }

    public sealed class FullPocketsAchievement : NetWinningsAchievement
    {
        public override string Id => "total_profit_10000";
        public override string Name => "盆满钵满";
        public override string Description => "累计赢利 $10,000。";
        public override int SortOrder => 100;
        public override int Target => 10000;
        public override Reward Reward => new Reward(3000);
        protected override bool Cumulative => true;
    }

    public sealed class FortuneFlowsAchievement : NetWinningsAchievement
    {
        public override string Id => "total_profit_100000";
        public override string Name => "八方来财";
        public override string Description => "累计赢利 $100,000。";
        public override int SortOrder => 101;
        public override int Target => 100000;
        public override Reward Reward => new Reward(10000);
        protected override bool Cumulative => true;
    }

    public sealed class MillionaireAchievement : NetWinningsAchievement
    {
        public override string Id => "total_profit_1000000";
        public override string Name => "大富翁";
        public override string Description => "累计赢利 $1,000,000。";
        public override int SortOrder => 102;
        public override int Target => 1000000;
        public override Reward Reward => new Reward(50000);
        protected override bool Cumulative => true;
    }

    public sealed class PokerKingAchievement : NetWinningsAchievement
    {
        public override string Id => "hand_profit_10000";
        public override string Name => "赌王";
        public override string Description => "一手牌净赚 $10,000。";
        public override int SortOrder => 103;
        public override int Target => 10000;
        public override Reward Reward => new Reward(2000);
        protected override bool Cumulative => false;
    }

    public sealed class PokerEmperorAchievement : NetWinningsAchievement
    {
        public override string Id => "hand_profit_50000";
        public override string Name => "赌帝";
        public override string Description => "一手牌净赚 $50,000。";
        public override int SortOrder => 104;
        public override int Target => 50000;
        public override Reward Reward => new Reward(5000);
        protected override bool Cumulative => false;
    }

    public sealed class PokerGodAchievement : NetWinningsAchievement
    {
        public override string Id => "hand_profit_100000";
        public override string Name => "赌神";
        public override string Description => "一手牌净赚 $100,000。";
        public override int SortOrder => 105;
        public override int Target => 100000;
        public override Reward Reward => new Reward(10000);
        protected override bool Cumulative => false;
    }
}
