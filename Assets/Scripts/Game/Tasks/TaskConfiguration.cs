using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Facts;

namespace Game.Tasks
{
    public sealed class TaskDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string RawJson { get; set; }
        public int MaxCompletions { get; set; }
        public GameTask Rule { get; set; }
    }

    public sealed class TaskPoolEntry
    {
        public string TaskId { get; set; }
        public bool Enabled { get; set; } = true;
        public int SortOrder { get; set; }
        public int ValidityDays { get; set; } = 1;
        public double Weight { get; set; }
    }

    public sealed class TaskCatalog
    {
        public string Revision { get; private set; }
        public IReadOnlyDictionary<string, TaskDefinition> Definitions { get; private set; }
        public IReadOnlyList<TaskPoolEntry> Fixed { get; private set; }
        public IReadOnlyList<TaskPoolEntry> Random { get; private set; }
        public int Capacity { get; private set; }
        public IReadOnlyDictionary<string, uint> ItemLimits { get; private set; }

        public static TaskCatalog Load(string definitions, string fixedPool, string randomPool,
            Func<string, uint?> itemLimit = null)
        {
            using var master = JsonDocument.Parse(definitions);
            using var fixedDoc = JsonDocument.Parse(fixedPool);
            using var randomDoc = JsonDocument.Parse(randomPool);
            TaskFactory.Fields(master.RootElement, "schemaVersion", "revision", "tasks");
            TaskFactory.Fields(fixedDoc.RootElement, "schemaVersion", "revision", "entries");
            TaskFactory.Fields(randomDoc.RootElement, "schemaVersion", "revision", "capacity", "entries");
            var revision = master.RootElement.GetProperty("revision").GetString();
            if (string.IsNullOrWhiteSpace(revision) || revision.Length > 64) throw new ArgumentException("Invalid task revision.");
            foreach (var doc in new[] { master, fixedDoc, randomDoc })
                if (doc.RootElement.GetProperty("schemaVersion").GetInt32() != 2 ||
                    doc.RootElement.GetProperty("revision").GetString() != revision)
                    throw new ArgumentException("All three task tables require schemaVersion=2 and the same revision.");
            var map = new Dictionary<string, TaskDefinition>(StringComparer.Ordinal);
            foreach (var element in master.RootElement.GetProperty("tasks").EnumerateArray())
            {
                TaskDefinition definition;
                try { definition = TaskFactory.Compile(element.GetRawText()); }
                catch (Exception error) { throw new ArgumentException("TaskDefinitions.json: " + element.GetRawText() + ": " + error.Message, error); }
                if (map.ContainsKey(definition.Id)) throw new ArgumentException("Duplicate task ID: " + definition.Id);
                map.Add(definition.Id, definition);
            }
            if (map.Count > 500) throw new ArgumentException("At most 500 task definitions are supported.");
            var fixedEntries = ReadEntries(fixedDoc.RootElement, false, map);
            var randomEntries = ReadEntries(randomDoc.RootElement, true, map);
            var overlap = fixedEntries.Select(e => e.TaskId).Intersect(randomEntries.Select(e => e.TaskId)).FirstOrDefault();
            if (overlap != null) throw new ArgumentException("FixedTasks.json / RandomTasks.json duplicate task: " + overlap);
            var capacity = randomDoc.RootElement.GetProperty("capacity").GetInt32();
            if (capacity < 0 || capacity > 100) throw new ArgumentException("RandomTasks.json capacity must be 0..100.");
            var limits = new Dictionary<string, uint>(StringComparer.Ordinal);
            foreach (var definition in map.Values)
            foreach (var item in definition.Rule.GetReward().Items)
            {
                var limit = itemLimit == null ? uint.MaxValue : itemLimit(item.ItemId);
                if (!limit.HasValue || limit.Value < item.Quantity)
                    throw new ArgumentException("Task " + definition.Id + " reward has an unknown item or exceeds its limit: " + item.ItemId);
                limits[item.ItemId] = limit.Value;
            }
            return new TaskCatalog
            {
                Revision = revision, Definitions = map, Fixed = fixedEntries, Random = randomEntries,
                Capacity = capacity, ItemLimits = limits
            };
        }

        private static TaskPoolEntry[] ReadEntries(JsonElement root, bool random, IDictionary<string, TaskDefinition> definitions)
        {
            var result = new List<TaskPoolEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in root.GetProperty("entries").EnumerateArray())
            {
                TaskFactory.Fields(row, random ? new[] { "taskId", "enabled", "weight", "validityDays" }
                    : new[] { "taskId", "enabled", "sortOrder" });
                var id = row.GetProperty("taskId").GetString();
                if (id == null || !definitions.ContainsKey(id) || !ids.Add(id))
                    throw new ArgumentException("Unknown or duplicate pool task ID: " + id);
                var entry = new TaskPoolEntry { TaskId = id, Enabled = row.GetProperty("enabled").GetBoolean() };
                if (random)
                {
                    entry.ValidityDays = TaskFactory.Positive(row.GetProperty("validityDays"), 3650);
                    entry.Weight = row.GetProperty("weight").GetDouble();
                    if (double.IsNaN(entry.Weight) || double.IsInfinity(entry.Weight) || entry.Weight < 0)
                        throw new ArgumentException("Random task weight must be finite and nonnegative: " + id);
                    if (definitions[id].Rule.IsDirectClaim && entry.ValidityDays != 1)
                        throw new ArgumentException("Direct-claim tasks must expire after one business day: " + id);
                }
                else entry.SortOrder = row.GetProperty("sortOrder").GetInt32();
                result.Add(entry);
            }
            return result.ToArray();
        }
    }

    public static class TaskFactory
    {
        public static readonly string[] TypeNames =
        {
            "DirectClaim", "DirectClaimWhenLesserThan", "DirectClaimWhenAtMost", "PlayHands",
            "FormPattern", "WinHands", "WinWithPattern", "CountHands", "AllObjectives"
        };

        public static TaskDefinition Compile(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Fields(root, "id", "name", "description", "type", "params", "maxCompletions");
            var id = root.GetProperty("id").GetString();
            if (!ValidId(id)) throw new ArgumentException("Invalid task ID.");
            var name = root.GetProperty("name").GetString();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100) throw new ArgumentException("Task name must be 1..100 characters.");
            var type = root.GetProperty("type").GetString();
            var parameters = root.GetProperty("params").EnumerateArray().ToArray();
            var count = type switch
            {
                "DirectClaim" => 1,
                "DirectClaimWhenLesserThan" or "DirectClaimWhenAtMost" or "PlayHands" or "WinHands" or "AllObjectives" => 2,
                "FormPattern" or "WinWithPattern" or "CountHands" => 3,
                _ => throw new ArgumentException("Unknown task type: " + type)
            };
            if (parameters.Length != count) throw new ArgumentException(type + " params requires exactly " + count + " arguments.");
            var reward = ReadReward(parameters[0]);
            GameTask rule = type switch
            {
                "DirectClaim" => new DirectClaim(reward),
                "DirectClaimWhenLesserThan" => new DirectClaimWhenLesserThan(reward, parameters[1].GetUInt32()),
                "DirectClaimWhenAtMost" => new DirectClaimWhenAtMost(reward, parameters[1].GetUInt32()),
                "PlayHands" => new PlayHands(reward, Positive(parameters[1])),
                "WinHands" => new WinHands(reward, Positive(parameters[1])),
                "FormPattern" => new FormPattern(reward, ReadPattern(parameters[1]), Positive(parameters[2])),
                "WinWithPattern" => new WinWithPattern(reward, ReadPattern(parameters[1]), Positive(parameters[2])),
                "CountHands" => new CountHands(reward, Positive(parameters[1]), ReadConditions(parameters[2])),
                "AllObjectives" => new AllObjectives(reward, ReadObjectives(parameters[1])),
                _ => throw new ArgumentException("Unknown task type.")
            };
            var description = root.TryGetProperty("description", out var text) ? text.GetString() : rule.Description;
            if (description == null || description.Length > 500) throw new ArgumentException("Task description exceeds 500 characters.");
            return new TaskDefinition
            {
                Id = id, Name = name, Description = description, Rule = rule, RawJson = root.GetRawText(),
                MaxCompletions = Positive(root.GetProperty("maxCompletions"), 1000)
            };
        }

        public static Reward ReadReward(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Number) return new Reward(value.GetUInt32());
            Fields(value, "chip", "items");
            var items = new List<ItemStack>();
            foreach (var item in value.GetProperty("items").EnumerateArray())
            {
                Fields(item, "itemId", "count");
                var id = item.GetProperty("itemId").GetString();
                if (!ValidId(id)) throw new ArgumentException("Invalid reward item ID.");
                items.Add(new ItemStack(id, item.GetProperty("count").GetUInt32()));
            }
            if (items.Count > 100) throw new ArgumentException("At most 100 reward item entries.");
            return new Reward(value.GetProperty("chip").GetUInt32(), items);
        }

        private static ObjectivePlan[] ReadObjectives(JsonElement array)
        {
            var objectives = new List<ObjectivePlan>();
            foreach (var value in array.EnumerateArray())
            {
                Fields(value, "id", "type", "params");
                if (value.GetProperty("type").GetString() != "CountDistinctHands") throw new ArgumentException("Unsupported objective type.");
                var id = value.GetProperty("id").GetString();
                if (!ValidId(id)) throw new ArgumentException("Invalid objective ID.");
                var args = value.GetProperty("params").EnumerateArray().ToArray();
                if (args.Length != 2) throw new ArgumentException("CountDistinctHands params requires target and conditions.");
                objectives.Add(new ObjectivePlan(id, Positive(args[0]), ReadConditions(args[1])));
            }
            if (objectives.Count == 0 || objectives.Count > 8) throw new ArgumentException("Expected 1..8 objectives.");
            return objectives.ToArray();
        }

        private static IHandCondition[] ReadConditions(JsonElement array)
        {
            var result = new List<IHandCondition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in array.EnumerateArray())
            {
                Fields(value, "type", "params");
                var type = value.GetProperty("type").GetString();
                var args = value.GetProperty("params").EnumerateArray().ToArray();
                IHandCondition condition;
                if (type == "PatternObserved" && args.Length == 1) condition = new PatternObservedCondition(ReadPattern(args[0]));
                else if (type == "Won" && args.Length == 0) condition = new WonCondition();
                else if (type == "HandSettled" && args.Length == 0) condition = new HandSettledCondition();
                else throw new ArgumentException("Unknown condition or invalid params: " + type);
                if (!seen.Add(condition.Description)) throw new ArgumentException("Duplicate condition: " + type);
                result.Add(condition);
            }
            if (result.Count == 0 || result.Count > 10) throw new ArgumentException("Expected 1..10 conditions.");
            return result.ToArray();
        }

        public static FormedPattern ReadPattern(JsonElement value)
        {
            var name = value.GetString();
            if (!Enum.TryParse(name, false, out FormedPattern pattern) || pattern == FormedPattern.None ||
                !Enum.IsDefined(typeof(FormedPattern), pattern) || pattern.ToString() != name)
                throw new ArgumentException("Unsupported pattern: " + name);
            return pattern;
        }
        public static int Positive(JsonElement value, int maximum = 100000)
        {
            var number = value.GetInt32();
            if (number < 1 || number > maximum) throw new ArgumentException("Expected positive integer <= " + maximum);
            return number;
        }
        public static bool ValidId(string id) => id != null && Regex.IsMatch(id, "^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$");
        internal static void Fields(JsonElement value, params string[] allowed)
        {
            if (value.ValueKind != JsonValueKind.Object) throw new ArgumentException("Expected JSON object.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in value.EnumerateObject())
                if (!allowed.Contains(field.Name) || !seen.Add(field.Name))
                    throw new ArgumentException("Unknown or duplicate field: " + field.Name);
        }
    }

    public interface IWeightedTaskSelector
    {
        IReadOnlyList<TaskPoolEntry> Select(IEnumerable<TaskPoolEntry> candidates, int count);
    }

    public sealed class WeightedTaskSelector : IWeightedTaskSelector
    {
        private readonly Func<double> _next;
        public WeightedTaskSelector(Func<double> next = null)
        {
            var random = new Random();
            _next = next ?? random.NextDouble;
        }
        public IReadOnlyList<TaskPoolEntry> Select(IEnumerable<TaskPoolEntry> candidates, int count)
        {
            var remaining = candidates.Where(e => e.Enabled && e.Weight > 0).ToList();
            var result = new List<TaskPoolEntry>();
            while (result.Count < count && remaining.Count > 0)
            {
                var scale = remaining.Max(e => e.Weight);
                var unit = _next();
                if (unit < 0 || unit >= 1 || double.IsNaN(unit)) throw new InvalidOperationException("Random value must be in [0,1).");
                var value = unit * remaining.Sum(e => e.Weight / scale);
                var selected = remaining.Count - 1;
                for (var i = 0; i < remaining.Count; i++)
                {
                    value -= remaining[i].Weight / scale;
                    if (value < 0) { selected = i; break; }
                }
                result.Add(remaining[selected]);
                remaining.RemoveAt(selected);
            }
            return result;
        }
    }
}
