using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when the final revealed best hand is exactly a straight.</summary>
    public sealed class ShowdownStraightAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_straight";
        public override string Name => "都连起来吧";
        public override string Description => "摊牌时，最佳牌型为顺子。";
        public override int SortOrder => 203;
        public override Reward Reward => new Reward(3000);
        protected override PokerHandRank Rank => PokerHandRank.Straight;
    }
}
