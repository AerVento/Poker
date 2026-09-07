using System;
using System.Globalization;

namespace Game.UI
{
    public static class ChipAmountFormatter
    {
        public static string Format(uint amount)
        {
            return amount < 1000000U
                ? amount.ToString(CultureInfo.InvariantCulture)
                : decimal.Round(amount / 1000m, 1, MidpointRounding.ToEven)
                    .ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }
    }
}
