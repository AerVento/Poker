using System.Globalization;

namespace Game.UI
{
    public static class SlotProbabilityText
    {
        public static string Format(double probability)
        {
            // Two decimals at most; a nonzero chance never rounds down to 0%.
            if (probability > 0 && probability < 0.00005) return "<0.01%";
            return (probability * 100d).ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }
        public static string TierName(SlotPrizeTier tier) => tier == SlotPrizeTier.Small ? "小奖" : tier == SlotPrizeTier.Medium ? "中等奖" : "大奖";
    }
}
