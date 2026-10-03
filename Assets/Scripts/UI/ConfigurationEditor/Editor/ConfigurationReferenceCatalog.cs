using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Framework.Configs;
using UnityEditor;

namespace Game.UI
{
    internal sealed class ConfigurationReferenceOption
    {
        internal string Id { get; }
        internal string Name { get; }
        internal ConfigurationReferenceOption(string id, string name) { Id = id; Name = name ?? ""; }
        internal bool Matches(string search) => Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
            Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal sealed class ConfigurationReferenceSnapshot
    {
        internal ConfigurationReferenceOption[] Options { get; }
        internal string Error { get; }
        private readonly HashSet<string> _ids;
        internal ConfigurationReferenceSnapshot(IEnumerable<ConfigurationReferenceOption> options, string error = null)
        {
            Options = options.Where(o => !string.IsNullOrWhiteSpace(o.Id)).GroupBy(o => o.Id, StringComparer.Ordinal)
                .Select(g => g.First()).OrderBy(o => o.Id, StringComparer.Ordinal).ToArray();
            _ids = new HashSet<string>(Options.Select(o => o.Id), StringComparer.Ordinal);
            Error = error;
        }
        internal bool Contains(string id) => _ids.Contains(id ?? "");
        internal ConfigurationReferenceOption[] Search(string search) => Options.Where(o => o.Matches(search ?? "")).ToArray();
    }

    internal sealed class ConfigurationReferenceCatalog
    {
        internal static readonly ConfigurationReferenceCatalog Current = new ConfigurationReferenceCatalog(ConfigManager.RootDirectory);
        private readonly string _directory;
        private readonly Dictionary<ConfigReferenceKind, ConfigurationReferenceSnapshot> _snapshots = new Dictionary<ConfigReferenceKind, ConfigurationReferenceSnapshot>();
        private readonly Dictionary<ConfigReferenceKind, double> _nextRead = new Dictionary<ConfigReferenceKind, double>();

        internal ConfigurationReferenceCatalog(string directory) => _directory = directory;

        internal ConfigurationReferenceSnapshot Get(ConfigReferenceKind kind, bool refresh = false)
        {
            var now = EditorApplication.timeSinceStartup;
            if (!refresh && _nextRead.TryGetValue(kind, out var next) && now < next) return _snapshots[kind];
            _nextRead[kind] = now + 1;
            var file = kind == ConfigReferenceKind.Item ? "Items/Items.json" : "DailyTasks/TaskDefinitions.json";
            try
            {
                var path = Path.Combine(_directory, file);
                var options = kind == ConfigReferenceKind.Item
                    ? ConfigManager.LoadAtPath<ItemsConfigData>(path).Where(item => item != null).Select(item => new ConfigurationReferenceOption(item.Id, item.Name))
                    : ConfigManager.LoadAtPath<TaskDefinitionsConfigData>(path).Where(task => task != null).Select(task => new ConfigurationReferenceOption(task.Id, task.Name));
                return _snapshots[kind] = new ConfigurationReferenceSnapshot(options);
            }
            catch (Exception)
            {
                // Discard stale candidates when the source is missing or no longer readable.
                return _snapshots[kind] = new ConfigurationReferenceSnapshot(Array.Empty<ConfigurationReferenceOption>(), "无法读取 " + file);
            }
        }
    }
}
