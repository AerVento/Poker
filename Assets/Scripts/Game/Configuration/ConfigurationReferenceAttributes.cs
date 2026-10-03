using System;

namespace Game
{
    public enum ConfigReferenceKind { Item, Task }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public abstract class ConfigReferenceAttribute : Attribute
    {
        public ConfigReferenceKind Kind { get; }
        protected ConfigReferenceAttribute(ConfigReferenceKind kind) => Kind = kind;
    }

    public sealed class ItemIdAttribute : ConfigReferenceAttribute
    {
        public ItemIdAttribute() : base(ConfigReferenceKind.Item) { }
    }

    public sealed class TaskIdAttribute : ConfigReferenceAttribute
    {
        public TaskIdAttribute() : base(ConfigReferenceKind.Task) { }
    }
}
