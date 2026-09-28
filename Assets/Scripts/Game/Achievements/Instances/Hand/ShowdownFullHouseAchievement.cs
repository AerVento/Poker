using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when the final revealed best hand is exactly a full house.</summary>
    public sealed class ShowdownFullHouseAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_full_house";
        public override string Name => "爷爷！爷爷！";
        public override string Description => "摊牌时，最佳牌型为葫芦。";
        public override int SortOrder => 205;
        public override Reward Reward => new Reward(5000);
        public override string IconPath => "UI/Achievements/FullHouse";
        protected override PokerHandRank Rank => PokerHandRank.Full_House;
    }
}
