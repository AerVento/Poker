using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when the active contract sentence reaches five centuries.</summary>
    public sealed class FiveCenturySentenceAchievement : SentenceYearsAchievement
    {
        public override string Id => "sentence_years_500";
        public override string Name => "五百年劫";
        public override string Description => "刑期达到 500 年。";
        public override int SortOrder => 15;
        public override int Target => 500;
        public override Reward Reward => new Reward(2000);
    }
}
