using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when the final revealed best hand is exactly two pair.</summary>
    public sealed class ShowdownTwoPairAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_two_pair";
        public override string Name => "对对对碰";
        public override string Description => "摊牌时，最佳牌型为两对。";
        public override int SortOrder => 201;
        public override Reward Reward => new Reward(1000);
        protected override PokerHandRank Rank => PokerHandRank.Two_Pair;
    }
}
