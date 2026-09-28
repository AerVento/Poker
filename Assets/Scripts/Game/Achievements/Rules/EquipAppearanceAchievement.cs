using Game.Events;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Shared evaluator for equipping a non-default appearance in a specific collection slot.</summary>
    public abstract class EquipAppearanceAchievement : Achievement, IAchievementHandler<EquipmentChangedEvent>
    {
        protected abstract string Slot { get; }
        public override string Category => AchievementCategories.General;
        public override Reward Reward => new Reward(1000);
        public override string IconPath => "UI/Achievements/Cards-v1";

        public void Evaluate(EquipmentChangedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            if (playerEvent.AccountId == accountId && playerEvent.Slot == Slot &&
                playerEvent.ItemId != "default" && playerEvent.ItemId != playerEvent.PreviousItemId) progress.Count = Target;
        }
    }
}
