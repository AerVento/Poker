using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes once current net assets first reach $1,000,000.</summary>
    public sealed class NationalMagnateAchievement : NetAssetsAchievement
    {
        public override string Id => "net_assets_1000000";
        public override string Name => "富可敌国";
        public override string Description => "净资产达到 $1,000,000。";
        public override int SortOrder => 108;
        public override int Target => 1000000;
        public override Reward Reward => new Reward(100000);
    }
}
