using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when the final revealed best hand is exactly a straight flush.</summary>
    public sealed class ShowdownStraightFlushAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_straight_flush";
        public override string Name => "“同花打不打得过俘虏豪斯？”";
        public override string Description => "摊牌时，最佳牌型为同花顺。";
        public override int SortOrder => 207;
        public override Reward Reward => new Reward(20000, new[] { new ItemStack("namecard-straight-flush", 1) });
        protected override PokerHandRank Rank => PokerHandRank.Straight_Flush;
    }
}
