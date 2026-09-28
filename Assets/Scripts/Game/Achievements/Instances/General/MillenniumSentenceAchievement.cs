using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when the active contract sentence reaches one thousand years.</summary>
    public sealed class MillenniumSentenceAchievement : SentenceYearsAchievement
    {
        public override string Id => "sentence_years_1000";
        public override string Name => "千载沉沦";
        public override string Description => "刑期达到 1,000 年。";
        public override int SortOrder => 16;
        public override int Target => 1000;
        public override Reward Reward => new Reward(3000);
    }
}
