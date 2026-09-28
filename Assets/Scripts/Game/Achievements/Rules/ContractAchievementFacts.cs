using System.Linq;
using Game.Events;

namespace Game.Achievements
{
    /// <summary>Centralizes the contract inventory facts shared by account and asset achievements.</summary>
    internal static class ContractAchievementFacts
    {
        public static bool IsContract(string id) => id == "devil-contract" || id == "archdevil-contract" || id == "satan-contract";

        public static ulong Years(InventoryChangedEvent value) => InfernalNamecard.CalculateYears(
            value.Items.FirstOrDefault(item => item.ItemId == "devil-contract")?.Quantity ?? 0,
            value.Items.FirstOrDefault(item => item.ItemId == "archdevil-contract")?.Quantity ?? 0,
            value.Items.FirstOrDefault(item => item.ItemId == "satan-contract")?.Quantity ?? 0);
    }
}
