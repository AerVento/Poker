using System;

namespace Game
{
    public enum CurrencyResult { Success, InsufficientFunds, BalanceLimit, ExchangeLocked }

    // Shared currency rules. The server persists each plan inside the owning operation's transaction.
    public static class CurrencySystem
    {
        public static ulong PurchasingPower(uint dollars, uint availableChips, bool canExchange)
            => (ulong)dollars + (canExchange ? availableChips : 0UL);

        public static bool CanAfford(ulong cost, uint dollars, uint availableChips, bool canExchange)
            => cost <= uint.MaxValue && cost <= PurchasingPower(dollars, availableChips, canExchange);

        public static CurrencyResult PlanPayment(uint dollars, uint availableChips, bool canExchange,
            ulong cost, out uint chipConversion)
        {
            chipConversion = 0;
            if (cost > uint.MaxValue) return CurrencyResult.BalanceLimit;
            if (cost <= dollars) return CurrencyResult.Success;
            if (!CanAfford(cost, dollars, availableChips, canExchange)) return CurrencyResult.InsufficientFunds;
            chipConversion = (uint)(cost - dollars);
            return CurrencyResult.Success;
        }

        public static CurrencyResult PlanExchange(uint dollars, uint chips, uint availableChips,
            bool canExchange, bool dollarsToChips, uint amount, out uint nextDollars, out uint nextChips)
        {
            nextDollars = dollars;
            nextChips = chips;
            if (!canExchange) return CurrencyResult.ExchangeLocked;
            if (amount > (dollarsToChips ? dollars : Math.Min(chips, availableChips)))
                return CurrencyResult.InsufficientFunds;
            if ((ulong)(dollarsToChips ? chips : dollars) + amount > uint.MaxValue)
                return CurrencyResult.BalanceLimit;
            nextDollars = dollarsToChips ? dollars - amount : dollars + amount;
            nextChips = dollarsToChips ? chips + amount : chips - amount;
            return CurrencyResult.Success;
        }

        public static CurrencyResult PlanBalanceChange(uint balance, long delta, out uint next)
        {
            next = balance;
            if (delta < -(long)balance) return CurrencyResult.InsufficientFunds;
            if (delta > (long)uint.MaxValue - balance) return CurrencyResult.BalanceLimit;
            next = (uint)(balance + delta);
            return CurrencyResult.Success;
        }
    }
}
