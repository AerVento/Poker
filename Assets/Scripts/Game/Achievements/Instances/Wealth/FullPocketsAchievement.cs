using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Rewards the first $10,000 of lifetime positive net winnings.</summary>
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
}
