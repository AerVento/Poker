using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Rewards $1,000,000 in lifetime winnings, distinct from net-asset milestones.</summary>
    public sealed class MillionaireAchievement : NetWinningsAchievement
    {
        public override string Id => "total_profit_1000000";
        public override string Name => "百万赢家";
        public override string Description => "累计赢利 $1,000,000。";
        public override int SortOrder => 102;
        public override int Target => 1000000;
        public override Reward Reward => new Reward(50000);
        protected override bool Cumulative => true;
    }
}
