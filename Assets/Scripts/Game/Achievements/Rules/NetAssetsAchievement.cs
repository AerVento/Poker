using System;
using System.Linq;
using Game.Events;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Tracks current net assets from an authoritative balance-and-contract inventory snapshot.</summary>
    public abstract class NetAssetsAchievement : Achievement, IAchievementHandler<AccountAssetsChangedEvent>
    {
        public override string Category => AchievementCategories.Wealth;
        public override string IconPath => "UI/Achievements/Wealth-v1";

        public void Evaluate(AccountAssetsChangedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            if (playerEvent.AccountId != accountId) return;
            var assets = (long)playerEvent.Dollars + playerEvent.Chips - ContractDebt(playerEvent.Items);
            // Before completion the display follows current assets, including debt. Completion remains permanent in the service.
            progress.Count = assets > int.MaxValue ? int.MaxValue : assets < int.MinValue ? int.MinValue : (int)assets;
        }

        private static long ContractDebt(System.Collections.Generic.IReadOnlyList<ItemStack> items)
        {
            var devil = items.FirstOrDefault(item => item.ItemId == "devil-contract")?.Quantity ?? 0;
            var archdevil = items.FirstOrDefault(item => item.ItemId == "archdevil-contract")?.Quantity ?? 0;
            var satan = items.FirstOrDefault(item => item.ItemId == "satan-contract")?.Quantity ?? 0;
            return 1000L * devil + 10000L * archdevil + 100000L * satan;
        }
    }
}
