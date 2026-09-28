using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes once current net assets first reach $500,000.</summary>
    public sealed class RegionalMagnateAchievement : NetAssetsAchievement
    {
        public override string Id => "net_assets_500000";
        public override string Name => "富甲一方";
        public override string Description => "净资产达到 $500,000。";
        public override int SortOrder => 107;
        public override int Target => 500000;
        public override Reward Reward => new Reward(50000);
    }
}
