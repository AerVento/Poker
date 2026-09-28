using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when the final revealed best hand is exactly a flush.</summary>
    public sealed class ShowdownFlushAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_flush";
        public override string Name => "清一色";
        public override string Description => "摊牌时，最佳牌型为同花。";
        public override int SortOrder => 204;
        public override Reward Reward => new Reward(4000);
        protected override PokerHandRank Rank => PokerHandRank.Flush;
    }
}
