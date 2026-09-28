using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Rewards a single hand with at least $10,000 net profit.</summary>
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
}
