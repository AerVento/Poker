using System;
using System.Collections.Generic;
using System.Linq;
using Game.Tasks;

namespace Game.Achievements
{
    public static class AchievementCategories
    {
        public const string General = "通用";
        public const string Special = "特殊";
        public const string Wealth = "财富";
        public const string Hand = "牌型";
    }

    // Definitions are shared and stateless. All player-specific work belongs in progress.
    // Add a public, non-abstract class with a public parameterless constructor in this assembly.
    public abstract class Achievement
    {
        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public virtual string Category => AchievementCategories.General;
        public virtual string IconPath => "UI/Achievements/Default";
        public virtual int SortOrder => 0;
        public virtual int Target => 1;
        public abstract Reward Reward { get; }
        public abstract void Evaluate(AchievementContext context, string accountId, AchievementProgress progress);
    }

    public sealed class AchievementProgress
    {
        public int Count { get; set; }
        // Optional persistent scratch state for streaks, multi-step or cross-hand achievements.
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AchievementCatalog
    {
        public IReadOnlyList<Achievement> All { get; }
        private readonly Dictionary<string, Achievement> _byId;

        public AchievementCatalog(IEnumerable<Achievement> definitions)
        {
            var entries = definitions.ToArray();
            foreach (var entry in entries)
            {
                if (entry == null || !IsValidId(entry.Id) || string.IsNullOrWhiteSpace(entry.Name) ||
                    string.IsNullOrWhiteSpace(entry.Description) || string.IsNullOrWhiteSpace(entry.Category) ||
                    string.IsNullOrWhiteSpace(entry.IconPath) || entry.Target < 1)
                    throw new InvalidOperationException("Invalid achievement definition: " + entry?.GetType().FullName);
            }
            if (entries.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("Achievement IDs must be unique and permanent.");
            All = Array.AsReadOnly(entries.OrderBy(a => a.SortOrder).ThenBy(a => a.Id, StringComparer.Ordinal).ToArray());
            _byId = entries.ToDictionary(a => a.Id, StringComparer.Ordinal);
        }

        public Achievement Find(string id) => id != null && _byId.TryGetValue(id, out var value) ? value : null;

        public static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 64 &&
            id.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-');

        public static IEnumerable<Type> DiscoverTypes() => typeof(Achievement).Assembly.GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract && !t.ContainsGenericParameters && typeof(Achievement).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        public static AchievementCatalog Discover() => new AchievementCatalog(DiscoverTypes().Select(t =>
        {
            if (t.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException(t.FullName + " needs a public parameterless constructor.");
            return (Achievement)Activator.CreateInstance(t);
        }));
    }
}
