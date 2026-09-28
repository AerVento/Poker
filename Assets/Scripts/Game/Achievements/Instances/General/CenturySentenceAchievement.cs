using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when the active contract sentence reaches one century.</summary>
    public sealed class CenturySentenceAchievement : SentenceYearsAchievement
    {
        public override string Id => "sentence_years_100";
        public override string Name => "百年之囚";
        public override string Description => "刑期达到 100 年。";
        public override int SortOrder => 14;
        public override int Target => 100;
        public override Reward Reward => new Reward(1000);
    }
}
