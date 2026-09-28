using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when the final revealed best hand is exactly three of a kind.</summary>
    public sealed class ShowdownThreeOfAKindAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_three_of_a_kind";
        public override string Name => "三连";
        public override string Description => "摊牌时，最佳牌型为三条。";
        public override int SortOrder => 202;
        public override Reward Reward => new Reward(2000);
        protected override PokerHandRank Rank => PokerHandRank.Three_of_a_Kind;
    }
}
