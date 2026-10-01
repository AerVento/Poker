using Game.Events;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes after successfully purchasing any kind of demon contract.</summary>
    public sealed class SignDevilContractAchievement : Achievement, IAchievementHandler<ItemPurchasedEvent>
    {
        public override string Id => "sign_devil_contract";
        public override string Name => "与魔为契";
        public override string Description => "与任意恶魔签订契约。";
        public override int SortOrder => 12;
        public override string IconPath => "UI/Achievements/champion";
        public override Reward Reward => new Reward(2000);

        public void Evaluate(ItemPurchasedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            if (playerEvent.AccountId == accountId && playerEvent.ProductKind == "item" &&
                playerEvent.Quantity > 0 && ContractAchievementFacts.IsContract(playerEvent.ItemId)) progress.Count = Target;
        }
    }
}
