using Game.Events;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes after consuming one demon contract to repay its debt.</summary>
    public sealed class RepayDevilContractAchievement : Achievement, IAchievementHandler<ItemUsedEvent>
    {
        public override string Id => "repay_devil_contract";
        public override string Name => "还债之路";
        public override string Description => "使用一份恶魔契约，偿还债务。";
        public override int SortOrder => 13;
        public override string IconPath => "UI/Achievements/Wealth-v1";
        public override Reward Reward => new Reward(2000);

        public void Evaluate(ItemUsedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            if (playerEvent.AccountId == accountId && playerEvent.Quantity > 0 && ContractAchievementFacts.IsContract(playerEvent.ItemId))
                progress.Count = Target;
        }
    }
}
