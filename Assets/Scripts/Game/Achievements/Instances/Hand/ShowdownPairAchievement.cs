using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when the final revealed best hand is exactly one pair.</summary>
    public sealed class ShowdownPairAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_pair";
        public override string Name => "对对碰";
        public override string Description => "摊牌时，最佳牌型为一对。";
        public override int SortOrder => 200;
        public override Reward Reward => new Reward(500);
        protected override PokerHandRank Rank => PokerHandRank.Pair;
    }
}
