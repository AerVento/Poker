using Game.Events;

namespace Game.Achievements
{
    /// <summary>
    /// Centralizes the contract inventory facts shared by account and asset achievements. Contracts are configured in
    /// Configs/Items/Contracts.json; the server sets the catalog before it evaluates any achievement.
    /// </summary>
    internal static class ContractAchievementFacts
    {
        public static GameCatalog Catalog { get; set; }

        public static bool IsContract(string id) => Catalog?.FindContract(id) != null;

        public static ulong Years(InventoryChangedEvent value) => Catalog?.ServiceYears(value.Items) ?? 0;

        public static ulong Debt(System.Collections.Generic.IReadOnlyList<ItemStack> items) => Catalog?.ContractDebt(items) ?? 0;
    }
}
