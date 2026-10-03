using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Framework.Configs;
using Game.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Tasks
{
    public sealed class TaskDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int MaxCompletions { get; set; }
        public GameTask Rule { get; set; }
    }

    public sealed class TaskPoolEntry
    {
        public string TaskId { get; set; }
        public bool Enabled { get; set; } = true;
        public int ValidityDays { get; set; } = 1;
        public double Weight { get; set; }
    }

    /// <summary>The task tables loaded at server startup. Assigned tasks refer to these definitions by ID.</summary>
    public sealed class TaskCatalog
    {
        public IReadOnlyDictionary<string, TaskDefinition> Definitions { get; private set; }
        /// <summary>Fixed tasks in file order, which is also their display order.</summary>
        public IReadOnlyList<TaskPoolEntry> Fixed { get; private set; }
        public IReadOnlyList<TaskPoolEntry> Random { get; private set; }
        public int Capacity { get; private set; }
        public Func<string, uint?> CurrentItemLimit { get; private set; }

        public static TaskCatalog Build(TaskDefinitionsConfigData master, FixedTasksConfigData fixedPool,
            RandomTasksConfigData randomPool, Func<string, uint?> itemLimit = null)
        {
            var map = new Dictionary<string, TaskDefinition>(StringComparer.Ordinal);
            foreach (var element in master)
            {
                TaskDefinition definition;
                try { definition = TaskFactory.Compile(element); }
                catch (Exception error) { throw new ArgumentException("TaskDefinitions.json: " + element?.Id + ": " + error.Message, error); }
                if (map.ContainsKey(definition.Id)) throw new ArgumentException("Duplicate task ID: " + definition.Id);
                map.Add(definition.Id, definition);
            }
            if (map.Count > 500) throw new ArgumentException("At most 500 task definitions are supported.");
            var fixedEntries = ValidateEntries(fixedPool.Select(e => e == null ? null : new TaskPoolEntry { TaskId = e.TaskId, Enabled = e.Enabled }).ToArray(), false, map);
            var randomEntries = ValidateEntries(randomPool.Entries.Select(e => e == null ? null : new TaskPoolEntry { TaskId = e.TaskId, Enabled = e.Enabled, Weight = e.Weight, ValidityDays = e.ValidityDays }).ToArray(), true, map);
            var overlap = fixedEntries.Select(e => e.TaskId).Intersect(randomEntries.Select(e => e.TaskId)).FirstOrDefault();
            if (overlap != null) throw new ArgumentException("FixedTasks.json / RandomTasks.json duplicate task: " + overlap);
            var capacity = randomPool.Capacity;
            if (capacity < 0 || capacity > 100) throw new ArgumentException("RandomTasks.json capacity must be 0..100.");
            foreach (var definition in map.Values)
            {
                definition.Rule.GetReward().Validate(itemLimit ?? (_ => uint.MaxValue));
                foreach (var item in definition.Rule.GetReward().Items)
                    if (itemLimit != null && !itemLimit(item.ItemId).HasValue)
                        throw new ArgumentException("Task " + definition.Id + " reward has an unknown item: " + item.ItemId);
            }
            return new TaskCatalog
            {
                Definitions = map, Fixed = fixedEntries, Random = randomEntries, Capacity = capacity, CurrentItemLimit = itemLimit
            };
        }

        private static TaskPoolEntry[] ValidateEntries(TaskPoolEntry[] entries, bool random, IDictionary<string, TaskDefinition> definitions)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || entry.TaskId == null || !definitions.ContainsKey(entry.TaskId) || !ids.Add(entry.TaskId))
                    throw new ArgumentException("Unknown or duplicate pool task ID: " + entry?.TaskId);
                if (random)
                {
                    if (entry.ValidityDays < 1 || entry.ValidityDays > 3650) throw new ArgumentException("Expected validity-days 1..3650.");
                    if (double.IsNaN(entry.Weight) || double.IsInfinity(entry.Weight) || entry.Weight < 0)
                        throw new ArgumentException("Random task weight must be finite and nonnegative: " + entry.TaskId);
                    if (definitions[entry.TaskId].Rule.IsDirectClaim && entry.ValidityDays != 1)
                        throw new ArgumentException("Direct-claim tasks must expire after one business day: " + entry.TaskId);
                }
            }
            return entries;
        }
    }

    public static class TaskFactory
    {
        public static readonly string[] TypeNames = Enum.GetNames(typeof(TaskConfigKind));

        // params holds the positional arguments of the rule type; the reward is a field of its own.
        public static TaskDefinition Compile(TaskConfigEntry entry)
        {
            if (entry == null) throw new ArgumentException("Task entry cannot be null.");
            if (!ValidId(entry.Id)) throw new ArgumentException("Invalid task ID.");
            if (string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Length > 100) throw new ArgumentException("Task name must be 1..100 characters.");
            var parameters = entry.Parameters ?? Array.Empty<JToken>();
            var count = entry.Type switch
            {
                TaskConfigKind.DirectClaim => 0,
                TaskConfigKind.DirectClaimWhenLesserThan or TaskConfigKind.DirectClaimWhenAtMost or TaskConfigKind.PlayHands or
                    TaskConfigKind.WinHands or TaskConfigKind.AllObjectives => 1,
                TaskConfigKind.FormPattern or TaskConfigKind.WinWithPattern or TaskConfigKind.CountHands => 2,
                _ => throw new ArgumentException("Unknown task type: " + entry.Type)
            };
            if (parameters.Length != count) throw new ArgumentException(entry.Type + " params requires exactly " + count + " arguments.");
            var reward = RewardConfig.ToReward(entry.Reward);
            GameTask rule = entry.Type switch
            {
                TaskConfigKind.DirectClaim => new DirectClaim(reward),
                TaskConfigKind.DirectClaimWhenLesserThan => new DirectClaimWhenLesserThan(reward, Read<uint>(parameters[0])),
                TaskConfigKind.DirectClaimWhenAtMost => new DirectClaimWhenAtMost(reward, Read<uint>(parameters[0])),
                TaskConfigKind.PlayHands => new PlayHands(reward, Positive(parameters[0])),
                TaskConfigKind.WinHands => new WinHands(reward, Positive(parameters[0])),
                TaskConfigKind.FormPattern => new FormPattern(reward, ReadPattern(parameters[0]), Positive(parameters[1])),
                TaskConfigKind.WinWithPattern => new WinWithPattern(reward, ReadPattern(parameters[0]), Positive(parameters[1])),
                TaskConfigKind.CountHands => new CountHands(reward, Positive(parameters[0]), ReadConditions(parameters[1])),
                TaskConfigKind.AllObjectives => new AllObjectives(reward, ReadObjectives(parameters[0])),
                _ => throw new ArgumentException("Unknown task type.")
            };
            // An omitted description is generated from the rule.
            var description = entry.Description ?? rule.Description;
            if (description.Length > 500) throw new ArgumentException("Task description exceeds 500 characters.");
            if (entry.MaxCompletions < 1 || entry.MaxCompletions > 1000) throw new ArgumentException("max-completions must be 1..1000.");
            return new TaskDefinition { Id = entry.Id, Name = entry.Name, Description = description, Rule = rule, MaxCompletions = entry.MaxCompletions };
        }

        // Parameters go through the same field and type checks as whole configuration files.
        private static T Read<T>(JToken value) => ConfigManager.Deserialize<T>((value ?? JValue.CreateNull()).ToString(Formatting.None));

        private static ObjectivePlan[] ReadObjectives(JToken array)
        {
            var objectives = new List<ObjectivePlan>();
            foreach (var value in Read<TaskObjectiveConfigData[]>(array))
            {
                if (value == null || value.Type != TaskObjectiveKind.CountDistinctHands) throw new ArgumentException("Unsupported objective type.");
                if (!ValidId(value.Id)) throw new ArgumentException("Invalid objective ID.");
                var args = value.Parameters ?? Array.Empty<JToken>();
                if (args.Length != 2) throw new ArgumentException("CountDistinctHands params requires target and conditions.");
                objectives.Add(new ObjectivePlan(value.Id, Positive(args[0]), ReadConditions(args[1])));
            }
            if (objectives.Count == 0 || objectives.Count > 8) throw new ArgumentException("Expected 1..8 objectives.");
            return objectives.ToArray();
        }

        private static IHandCondition[] ReadConditions(JToken array)
        {
            var result = new List<IHandCondition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in Read<TaskConditionConfigData[]>(array))
            {
                var args = value?.Parameters ?? Array.Empty<JToken>();
                IHandCondition condition;
                if (value?.Type == TaskConditionKind.PatternObserved && args.Length == 1) condition = new PatternObservedCondition(ReadPattern(args[0]));
                else if (value?.Type == TaskConditionKind.Won && args.Length == 0) condition = new WonCondition();
                else if (value?.Type == TaskConditionKind.HandSettled && args.Length == 0) condition = new HandSettledCondition();
                else throw new ArgumentException("Unknown condition or invalid params: " + value?.Type);
                if (!seen.Add(condition.Description)) throw new ArgumentException("Duplicate condition: " + value.Type);
                result.Add(condition);
            }
            if (result.Count == 0 || result.Count > 10) throw new ArgumentException("Expected 1..10 conditions.");
            return result.ToArray();
        }

        public static FormedPattern ReadPattern(JToken value)
        {
            var name = value?.Type == JTokenType.String ? value.Value<string>() : null;
            if (!Enum.TryParse(name, false, out FormedPattern pattern) || pattern == FormedPattern.None ||
                !Enum.IsDefined(typeof(FormedPattern), pattern) || pattern.ToString() != name)
                throw new ArgumentException("Unsupported pattern: " + name);
            return pattern;
        }

        public static int Positive(JToken value, int maximum = 100000)
        {
            var number = Read<int>(value);
            if (number < 1 || number > maximum) throw new ArgumentException("Expected positive integer <= " + maximum);
            return number;
        }

        public static bool ValidId(string id) => id != null && Regex.IsMatch(id, "^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$");
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
