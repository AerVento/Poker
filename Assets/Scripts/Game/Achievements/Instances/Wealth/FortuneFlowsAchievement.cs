using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Rewards accumulating $100,000 in positive net winnings.</summary>
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
}
