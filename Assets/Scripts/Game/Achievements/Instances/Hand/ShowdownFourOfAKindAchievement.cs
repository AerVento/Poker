using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when the final revealed best hand is exactly four of a kind.</summary>
    public sealed class ShowdownFourOfAKindAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_four_of_a_kind";
        public override string Name => "炸弹人";
        public override string Description => "摊牌时，最佳牌型为四条。";
        public override int SortOrder => 206;
        public override Reward Reward => new Reward(8000);
        protected override PokerHandRank Rank => PokerHandRank.Four_of_a_Kind;
    }
}
