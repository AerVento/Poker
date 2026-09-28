using System;
using Game.Events;

namespace Game.Achievements
{
    /// <summary>Tracks the highest observed active contract sentence for a threshold achievement.</summary>
    public abstract class SentenceYearsAchievement : Achievement, IAchievementHandler<InventoryChangedEvent>
    {
        public override string IconPath => "UI/Achievements/Champion-v1";

        public void Evaluate(InventoryChangedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            if (playerEvent.AccountId != accountId) return;
            var years = ContractAchievementFacts.Years(playerEvent);
            progress.Count = Math.Max(progress.Count, (int)Math.Min((ulong)Target, years));
        }
    }
}
