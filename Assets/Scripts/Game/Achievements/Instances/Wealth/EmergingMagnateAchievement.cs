using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes once current net assets first reach $100,000.</summary>
    public sealed class EmergingMagnateAchievement : NetAssetsAchievement
    {
        public override string Id => "net_assets_100000";
        public override string Name => "初露富态";
        public override string Description => "净资产达到 $100,000。";
        public override int SortOrder => 106;
        public override int Target => 100000;
        public override Reward Reward => new Reward(10000);
    }
}
