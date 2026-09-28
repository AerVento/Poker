using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Rewards a single hand with at least $50,000 net profit.</summary>
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
}
