using System;
using System.Text.Json.Serialization;

namespace Game
{
    public enum ComparisonType : byte
    {
        Equal, NotEqual, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual
    }

    public enum PurchaseConditionKind : byte { Balance = 1 }

    // Read-only rule description shared by the server and its presentation projection.
    public sealed class PurchaseConditionDescriptor
    {
        public PurchaseConditionKind Kind { get; }
        public ComparisonType Comparison { get; }
        public uint Threshold { get; }
        public int Version { get; }

        [JsonConstructor]
        public PurchaseConditionDescriptor(PurchaseConditionKind kind, ComparisonType comparison, uint threshold, int version = 1)
        {
            if (kind != PurchaseConditionKind.Balance || !Enum.IsDefined(typeof(ComparisonType), comparison) || version != 1)
                throw new ArgumentException("Unsupported purchase condition.");
            Kind = kind;
            Comparison = comparison;
            Threshold = threshold;
            Version = version;
        }

        public bool Matches(uint balance) => Comparison switch
        {
            ComparisonType.Equal => balance == Threshold,
            ComparisonType.NotEqual => balance != Threshold,
            ComparisonType.LessThan => balance < Threshold,
            ComparisonType.LessThanOrEqual => balance <= Threshold,
            ComparisonType.GreaterThan => balance > Threshold,
            ComparisonType.GreaterThanOrEqual => balance >= Threshold,
            _ => false
        };

        public string RequirementText()
        {
            if (Comparison == ComparisonType.Equal && Threshold == 0)
                return "仅总资产为 $0 时可购买";
            var comparison = Comparison switch
            {
                ComparisonType.Equal => "为",
                ComparisonType.NotEqual => "不为",
                ComparisonType.LessThan => "低于",
                ComparisonType.LessThanOrEqual => "不高于",
                ComparisonType.GreaterThan => "高于",
                ComparisonType.GreaterThanOrEqual => "至少为",
                _ => throw new InvalidOperationException("Unsupported comparison.")
            };
            return $"仅可用美金与筹码合计{comparison} ${Threshold:N0} 时可购买";
        }
    }
}
