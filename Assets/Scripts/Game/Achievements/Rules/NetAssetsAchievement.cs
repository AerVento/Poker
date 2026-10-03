using System;
using Game.Events;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Tracks current net assets from an authoritative balance-and-contract inventory snapshot.</summary>
    public abstract class NetAssetsAchievement : Achievement, IAchievementHandler<AccountAssetsChangedEvent>
    {
        public override string Category => AchievementCategories.Wealth;
        public override string IconPath => "UI/Achievements/wealth";

        public void Evaluate(AccountAssetsChangedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            if (playerEvent.AccountId != accountId) return;
            var assets = (long)playerEvent.Dollars + playerEvent.Chips - (long)ContractAchievementFacts.Debt(playerEvent.Items);
            // Before completion the display follows current assets, including debt. Completion remains permanent in the service.
            progress.Count = assets > int.MaxValue ? int.MaxValue : assets < int.MinValue ? int.MinValue : (int)assets;
        }
    }
}
