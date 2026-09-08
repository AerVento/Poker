using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Tasks
{
    public readonly struct Reward
    {
        public uint Chip { get; }
        public IReadOnlyList<ItemStack> Items { get; }

        public Reward(uint chip, IEnumerable<ItemStack> items = null)
        {
            Chip = chip;
            var merged = new Dictionary<string, uint>(StringComparer.Ordinal);
            foreach (var item in items ?? Array.Empty<ItemStack>())
            {
                if (item == null || item.Quantity == 0) throw new ArgumentException("Reward item count must be positive.");
                merged.TryGetValue(item.ItemId, out var count);
                merged[item.ItemId] = checked(count + item.Quantity);
            }
            Items = Array.AsReadOnly(merged.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new ItemStack(p.Key, p.Value)).ToArray());
        }

        public static Reward operator +(Reward left, Reward right)
        {
            return new Reward(checked(left.Chip + right.Chip),
                (left.Items ?? Array.Empty<ItemStack>()).Concat(right.Items ?? Array.Empty<ItemStack>()));
        }
    }
}
