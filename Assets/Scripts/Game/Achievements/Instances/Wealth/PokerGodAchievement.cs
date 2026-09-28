using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Rewards a single hand with at least $100,000 net profit.</summary>
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
