using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game
{
    public enum RewardItemKind : byte { Dollars, Chips, Item }

    public sealed class RewardEntry
    {
        public RewardItemKind Kind { get; set; }
        public string ItemId { get; set; }
        public ulong Quantity { get; set; }
    }

    [JsonConverter(typeof(RewardJsonConverter))]
    public sealed class Reward
    {
        public static Reward Empty { get; } = new Reward(Array.Empty<RewardEntry>());
        public IReadOnlyList<RewardEntry> Entries { get; }
        public ulong Dollars => Total(RewardItemKind.Dollars);
        public ulong Chips => Total(RewardItemKind.Chips);
        public IReadOnlyList<ItemStack> Items => Entries.Where(e => e.Kind == RewardItemKind.Item)
            .Select(e => new ItemStack(e.ItemId, checked((uint)e.Quantity))).ToArray();

        public Reward(IEnumerable<RewardEntry> entries)
        {
            var merged = new Dictionary<(RewardItemKind, string), ulong>();
            foreach (var entry in entries ?? throw new ArgumentNullException(nameof(entries)))
            {
                if (entry == null || entry.Quantity == 0 || !Enum.IsDefined(typeof(RewardItemKind), entry.Kind) ||
                    (entry.Kind == RewardItemKind.Item ? string.IsNullOrWhiteSpace(entry.ItemId) : !string.IsNullOrEmpty(entry.ItemId)))
                    throw new ArgumentException("Invalid reward entry.");
                var key = (entry.Kind, entry.Kind == RewardItemKind.Item ? entry.ItemId : null);
                merged.TryGetValue(key, out var quantity);
                merged[key] = checked(quantity + entry.Quantity);
            }
            Entries = Array.AsReadOnly(merged.OrderBy(p => p.Key.Item1).ThenBy(p => p.Key.Item2, StringComparer.Ordinal)
                .Select(p => new RewardEntry { Kind = p.Key.Item1, ItemId = p.Key.Item2, Quantity = p.Value }).ToArray());
        }

        public Reward(uint dollars, IEnumerable<ItemStack> items = null) : this(
            (dollars == 0 ? Array.Empty<RewardEntry>() : new[] { new RewardEntry { Kind = RewardItemKind.Dollars, Quantity = dollars } })
            .Concat((items ?? Array.Empty<ItemStack>()).Select(i => new RewardEntry
                { Kind = RewardItemKind.Item, ItemId = i.ItemId, Quantity = i.Quantity }))) { }

        private ulong Total(RewardItemKind kind) => Entries.Where(e => e.Kind == kind).Aggregate(0UL, (n, e) => checked(n + e.Quantity));
        public static Reward operator +(Reward left, Reward right) => new Reward(
            (left ?? Empty).Entries.Concat((right ?? Empty).Entries));

        public void Validate(Func<string, uint?> itemLimit)
        {
            if (Entries.Count > 100) throw new ArgumentException("At most 100 reward entries.");
            foreach (var entry in Entries)
            {
                if (entry.Quantity > uint.MaxValue) throw new ArgumentException("Reward quantity exceeds supported maximum.");
                if (entry.Kind == RewardItemKind.Item && !itemLimit(entry.ItemId).HasValue)
                    throw new ArgumentException("Unknown reward item: " + entry.ItemId);
            }
        }
    }

    // The stored and network format of rewards (configuration files use RewardConfigEntry arrays instead).
    // Also reads single-entry slot receipts written before rewards were unified; always writes the shared format.
    public sealed class RewardJsonConverter : JsonConverter<Reward>
    {
        public override Reward Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return Parse(document.RootElement);
        }

        private static Reward Parse(JsonElement value)
        {
            if (value.TryGetProperty("Kind", out _)) return new Reward(new[] { ReadEntry(value) });
            CheckFields(value, "Entries");
            return new Reward(value.GetProperty("Entries").EnumerateArray().Select(ReadEntry));
        }

        private static RewardEntry ReadEntry(JsonElement value)
        {
            CheckFields(value, "Kind", "ItemId", "Quantity");
            var kind = value.GetProperty("Kind");
            return new RewardEntry
            {
                Kind = kind.ValueKind == JsonValueKind.String ? Enum.Parse<RewardItemKind>(kind.GetString(), false) : (RewardItemKind)kind.GetByte(),
                ItemId = value.TryGetProperty("ItemId", out var id) ? id.GetString() : null,
                Quantity = value.GetProperty("Quantity").GetUInt64()
            };
        }

        private static void CheckFields(JsonElement value, params string[] fields)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
                if (!fields.Contains(property.Name) || !seen.Add(property.Name))
                    throw new JsonException("Unknown or duplicate reward field: " + property.Name);
        }

        public override void Write(Utf8JsonWriter writer, Reward value, JsonSerializerOptions options)
        {
            writer.WriteStartObject(); writer.WriteStartArray("Entries");
            foreach (var entry in value.Entries)
            {
                writer.WriteStartObject(); writer.WriteString("Kind", entry.Kind.ToString());
                if (entry.Kind == RewardItemKind.Item) writer.WriteString("ItemId", entry.ItemId);
                writer.WriteNumber("Quantity", entry.Quantity); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
    }
}
