using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Game.Facts;
using Game.Network.Tasks;
using Game.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    public sealed class DailyTaskConfigurationEditor : EditorWindow
    {
        [Serializable] private sealed class Item { public string itemId = ""; public long count = 1; }
        [Serializable] private sealed class Condition { public string type = "PatternObserved"; public FormedPattern pattern = FormedPattern.FullHouse; }
        [Serializable] private sealed class Objective
        {
            public string id = "hands";
            public int target = 1;
            public List<Condition> conditions = new List<Condition> { new Condition() };
        }
        [Serializable] private sealed class Definition
        {
            public string id = "new_task", name = "新任务", description = "", type = "FormPattern";
            public int maxCompletions = 1, target = 1;
            public long chip = 1000, threshold = 1000;
            public FormedPattern pattern = FormedPattern.FullHouse;
            public List<Item> items = new List<Item>();
            public List<Condition> conditions = new List<Condition> { new Condition() };
            public List<Objective> objectives = new List<Objective> { new Objective() };
        }
        [Serializable] private sealed class Entry
        {
            public string taskId = "";
            public bool enabled = true;
            public int sortOrder, validityDays = 3;
            public double weight = 1;
        }
        [SerializeField] private string _directory;
        [SerializeField] private string _revision = "daily-tasks-v5";
        [SerializeField] private List<Definition> _definitions = new List<Definition>();
        [SerializeField] private List<Entry> _fixed = new List<Entry>(), _random = new List<Entry>();
        [SerializeField] private int _capacity = 2, _tab, _selected;
        [SerializeField] private bool _loaded;
        private Vector2 _scroll;
        private string _message;
        private bool _error;
        private static readonly string[] ConditionTypes = { "PatternObserved", "Won", "HandSettled" };
        private static readonly string[] Patterns = Enum.GetNames(typeof(FormedPattern)).Where(n => n != "None").ToArray();
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        [MenuItem("Poker/每日任务/配置编辑器")]
        public static void Open() => GetWindow<DailyTaskConfigurationEditor>("每日任务配置");

        private void OnEnable()
        {
            minSize = new Vector2(680, 560);
            if (!_loaded) { _directory = ServerTaskConfigurationLoader.DefaultDirectory; LoadTables(); }
        }

        private void OnGUI()
        {
            _directory = EditorGUILayout.TextField("配置目录", _directory);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新读取三表")) LoadTables();
                if (GUILayout.Button("校验配置")) ValidateOrSave(false);
                if (GUILayout.Button("校验并保存三表")) ValidateOrSave(true);
            }
            _revision = EditorGUILayout.TextField("三表版本", _revision);
            EditorGUILayout.HelpBox("保存后重启服务器生效。已有任务保留分配时的定义与奖励。随机任务按业务日有效期补空位，固定任务每日刷新且不占名额。", MessageType.Info);
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, _error ? MessageType.Error : MessageType.Info);
            _tab = GUILayout.Toolbar(_tab, new[] { "总任务列表", "固定任务", "随机任务" });
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_tab == 0) DrawDefinitions();
            else DrawPool(_tab == 2);
            EditorGUILayout.EndScrollView();
        }

        private void DrawDefinitions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_definitions.Count > 0)
                    _selected = EditorGUILayout.Popup("编辑任务", Mathf.Clamp(_selected, 0, _definitions.Count - 1),
                        _definitions.Select(d => d.id + " · " + d.name).ToArray());
                if (GUILayout.Button("添加", GUILayout.Width(60)))
                {
                    _definitions.Add(new Definition { id = "task_" + Guid.NewGuid().ToString("N").Substring(0, 8) });
                    _selected = _definitions.Count - 1;
                }
                if (_definitions.Count > 0 && GUILayout.Button("删除", GUILayout.Width(60)))
                {
                    _definitions.RemoveAt(_selected); _selected = 0; return;
                }
            }
            if (_definitions.Count == 0) return;
            var task = _definitions[_selected];
            task.id = EditorGUILayout.TextField("任务 ID", task.id);
            task.name = EditorGUILayout.TextField("显示名称", task.name);
            task.description = EditorGUILayout.TextField("说明（空白自动生成）", task.description);
            task.type = Choose("任务类 type", task.type, TaskFactory.TypeNames);
            task.maxCompletions = EditorGUILayout.IntField("可完成次数", task.maxCompletions);
            task.chip = EditorGUILayout.LongField("每轮奖励筹码", task.chip);
            for (var i = 0; i < task.items.Count; i++)
            {
                using var row = new EditorGUILayout.HorizontalScope();
                task.items[i].itemId = EditorGUILayout.TextField("道具 ID", task.items[i].itemId);
                task.items[i].count = EditorGUILayout.LongField(task.items[i].count, GUILayout.Width(100));
                if (GUILayout.Button("移除", GUILayout.Width(50))) { task.items.RemoveAt(i); break; }
            }
            if (GUILayout.Button("添加每轮奖励道具")) task.items.Add(new Item());
            if (task.type == "DirectClaimWhenLesserThan" || task.type == "DirectClaimWhenAtMost")
                task.threshold = EditorGUILayout.LongField(task.type.EndsWith("AtMost") ? "余额上限（含）" : "余额阈值（不含）", task.threshold);
            if (task.type == "FormPattern" || task.type == "WinWithPattern") task.pattern = ChoosePattern(task.pattern);
            if (task.type == "PlayHands" || task.type == "WinHands" || task.type == "FormPattern" || task.type == "WinWithPattern" || task.type == "CountHands")
                task.target = EditorGUILayout.IntField("每轮所需不同手牌数", task.target);
            if (task.type == "CountHands") DrawConditions(task.conditions);
            if (task.type == "AllObjectives")
            {
                EditorGUILayout.HelpBox("各子目标分别累计；全部达标才完成一轮。CountHands 中的条件则必须由同一手牌满足。", MessageType.Info);
                for (var i = 0; i < task.objectives.Count; i++)
                {
                    using var box = new EditorGUILayout.VerticalScope("box");
                    var objective = task.objectives[i];
                    objective.id = EditorGUILayout.TextField("子目标稳定 ID", objective.id);
                    objective.target = EditorGUILayout.IntField("所需不同手牌数", objective.target);
                    DrawConditions(objective.conditions);
                    if (GUILayout.Button("移除子目标")) { task.objectives.RemoveAt(i); break; }
                }
                if (GUILayout.Button("添加子目标")) task.objectives.Add(new Objective { id = "goal_" + task.objectives.Count });
            }
            try
            {
                var json = JsonSerializer.Serialize(DefinitionValue(task), JsonOptions);
                var compiled = TaskFactory.Compile(json);
                EditorGUILayout.HelpBox("目标预览：" + compiled.Rule.Description + "。领取后开启下一轮，等待领奖期间不累计。", MessageType.Info);
                EditorGUILayout.SelectableLabel(json, EditorStyles.textArea, GUILayout.Height(180));
            }
            catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
        }

        private static void DrawConditions(List<Condition> conditions)
        {
            for (var i = 0; i < conditions.Count; i++)
            {
                using var row = new EditorGUILayout.HorizontalScope();
                var condition = conditions[i];
                condition.type = Choose("同一手条件", condition.type, ConditionTypes);
                if (condition.type == "PatternObserved") condition.pattern = ChoosePattern(condition.pattern);
                if (GUILayout.Button("移除", GUILayout.Width(50))) { conditions.RemoveAt(i); break; }
            }
            if (GUILayout.Button("增加 AND 条件")) conditions.Add(new Condition());
        }

        private void DrawPool(bool random)
        {
            if (random) _capacity = EditorGUILayout.IntField("随机任务持有上限", _capacity);
            var entries = random ? _random : _fixed;
            var ids = _definitions.Select(d => d.id).ToArray();
            for (var i = 0; i < entries.Count; i++)
            {
                using var box = new EditorGUILayout.VerticalScope("box");
                var entry = entries[i];
                entry.taskId = Choose("任务 ID", entry.taskId, ids);
                entry.enabled = EditorGUILayout.Toggle("启用", entry.enabled);
                if (random)
                {
                    entry.weight = EditorGUILayout.DoubleField("相对权重（0 不出现）", entry.weight);
                    entry.validityDays = EditorGUILayout.IntField("有效业务日数", entry.validityDays);
                }
                else entry.sortOrder = EditorGUILayout.IntField("排序", entry.sortOrder);
                if (GUILayout.Button("移除引用")) { entries.RemoveAt(i); break; }
            }
            if (GUILayout.Button("添加任务引用")) entries.Add(new Entry { taskId = ids.FirstOrDefault() ?? "" });
        }

        private static string Choose(string label, string value, string[] choices)
        {
            // Preserve unknown IDs until validation; never silently replace a broken reference.
            if (!choices.Contains(value)) choices = new[] { value }.Concat(choices).ToArray();
            return choices[EditorGUILayout.Popup(label, Array.IndexOf(choices, value), choices)];
        }
        private static FormedPattern ChoosePattern(FormedPattern pattern) =>
            (FormedPattern)Enum.Parse(typeof(FormedPattern), Choose("牌型", pattern.ToString(), Patterns));

        private static object[] ConditionsValue(List<Condition> values) => values.Select(c => (object)new
            { type = c.type, @params = c.type == "PatternObserved" ? new object[] { c.pattern.ToString() } : Array.Empty<object>() }).ToArray();

        private static Dictionary<string, object> DefinitionValue(Definition task)
        {
            var reward = (object)new { chip = checked((uint)task.chip), items = task.items.Select(i => new { itemId = i.itemId, count = checked((uint)i.count) }).ToArray() };
            object[] args = task.type switch
            {
                "DirectClaim" => new[] { reward },
                "DirectClaimWhenLesserThan" or "DirectClaimWhenAtMost" => new[] { reward, checked((uint)task.threshold) },
                "PlayHands" or "WinHands" => new[] { reward, task.target },
                "FormPattern" or "WinWithPattern" => new[] { reward, task.pattern.ToString(), task.target },
                "CountHands" => new[] { reward, task.target, ConditionsValue(task.conditions) },
                "AllObjectives" => new[] { reward, task.objectives.Select(o => new { id = o.id, type = "CountDistinctHands", @params = new object[] { o.target, ConditionsValue(o.conditions) } }).ToArray() },
                _ => throw new ArgumentException("Unknown task type")
            };
            var result = new Dictionary<string, object> { ["id"] = task.id, ["name"] = task.name, ["type"] = task.type, ["params"] = args, ["maxCompletions"] = task.maxCompletions };
            if (!string.IsNullOrEmpty(task.description)) result["description"] = task.description;
            return result;
        }

        private string[] SerializeTables() => new[]
        {
            JsonSerializer.Serialize(new { schemaVersion = 2, revision = _revision, tasks = _definitions.Select(DefinitionValue).ToArray() }, JsonOptions),
            JsonSerializer.Serialize(new { schemaVersion = 2, revision = _revision, entries = _fixed.Select(e => new { taskId = e.taskId, enabled = e.enabled, sortOrder = e.sortOrder }).ToArray() }, JsonOptions),
            JsonSerializer.Serialize(new { schemaVersion = 2, revision = _revision, capacity = _capacity, entries = _random.Select(e => new { taskId = e.taskId, enabled = e.enabled, weight = e.weight, validityDays = e.validityDays }).ToArray() }, JsonOptions)
        };

        private void ValidateOrSave(bool save)
        {
            try
            {
                var texts = SerializeTables();
                ValidateWithItems(texts);
                if (save)
                {
                    Directory.CreateDirectory(_directory);
                    // Write complete files first; a revision mismatch is rejected by the server on restart.
                    for (var i = 0; i < texts.Length; i++) File.WriteAllText(Path.Combine(_directory, ServerTaskConfigurationLoader.FileNames[i] + ".tmp"), texts[i], new System.Text.UTF8Encoding(false));
                    foreach (var name in ServerTaskConfigurationLoader.FileNames)
                        File.Copy(Path.Combine(_directory, name + ".tmp"), Path.Combine(_directory, name), true);
                    foreach (var name in ServerTaskConfigurationLoader.FileNames) File.Delete(Path.Combine(_directory, name + ".tmp"));
                }
                _error = false; _message = save ? "三张表已保存。重启服务器后，新分配的任务使用此版本。" : "三张表校验通过。";
            }
            catch (Exception error) { _error = true; _message = error.Message; }
        }

        internal static TaskCatalog ValidateWithItems(string[] texts)
        {
            var asset = Resources.Load<TextAsset>("SO/Shop/ItemServerCatalog");
            var items = asset == null ? Array.Empty<Game.Network.Accounts.ServerItemDefinition>() :
                JsonSerializer.Deserialize<Game.Network.Accounts.ServerItemDefinition[]>(asset.text);
            return TaskCatalog.Load(texts[0], texts[1], texts[2], id => items.FirstOrDefault(i => i.Id == id)?.MaxQuantity);
        }

        private void LoadTables()
        {
            try
            {
                var texts = ServerTaskConfigurationLoader.FileNames.Select(n => File.ReadAllText(Path.Combine(_directory, n))).ToArray();
                var catalog = ValidateWithItems(texts);
                var definitions = new List<Definition>();
                foreach (var source in catalog.Definitions.Values)
                {
                    using var json = JsonDocument.Parse(source.RawJson);
                    var root = json.RootElement;
                    var args = root.GetProperty("params").EnumerateArray().ToArray();
                    var draft = new Definition { id = source.Id, name = source.Name, type = root.GetProperty("type").GetString(),
                        maxCompletions = source.MaxCompletions, description = root.TryGetProperty("description", out var desc) ? desc.GetString() : "",
                        chip = source.Rule.GetReward().Chip, items = source.Rule.GetReward().Items.Select(i => new Item { itemId = i.ItemId, count = i.Quantity }).ToList() };
                    if (draft.type == "DirectClaimWhenLesserThan" || draft.type == "DirectClaimWhenAtMost") draft.threshold = args[1].GetUInt32();
                    if (draft.type == "PlayHands" || draft.type == "WinHands" || draft.type == "CountHands") draft.target = args[1].GetInt32();
                    if (draft.type == "FormPattern" || draft.type == "WinWithPattern") { draft.pattern = TaskFactory.ReadPattern(args[1]); draft.target = args[2].GetInt32(); }
                    if (draft.type == "CountHands") draft.conditions = ReadConditions(args[2]);
                    if (draft.type == "AllObjectives") draft.objectives = args[1].EnumerateArray().Select(o => new Objective
                    {
                        id = o.GetProperty("id").GetString(), target = o.GetProperty("params")[0].GetInt32(), conditions = ReadConditions(o.GetProperty("params")[1])
                    }).ToList();
                    definitions.Add(draft);
                }
                _definitions = definitions;
                _fixed = catalog.Fixed.Select(e => new Entry { taskId = e.TaskId, enabled = e.Enabled, sortOrder = e.SortOrder }).ToList();
                _random = catalog.Random.Select(e => new Entry { taskId = e.TaskId, enabled = e.Enabled, weight = e.Weight, validityDays = e.ValidityDays }).ToList();
                _capacity = catalog.Capacity; _revision = catalog.Revision; _selected = 0; _loaded = true; _error = false; _message = "已读取三张配置表。";
            }
            catch (Exception error) { _error = true; _message = error.Message; }
        }

        private static List<Condition> ReadConditions(JsonElement array) => array.EnumerateArray().Select(c => new Condition
        {
            type = c.GetProperty("type").GetString(), pattern = c.GetProperty("type").GetString() == "PatternObserved" ? TaskFactory.ReadPattern(c.GetProperty("params")[0]) : FormedPattern.FullHouse
        }).ToList();
    }

    public sealed class DailyTaskConfigurationBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            var directory = ServerTaskConfigurationLoader.DefaultDirectory;
            try { DailyTaskConfigurationEditor.ValidateWithItems(ServerTaskConfigurationLoader.FileNames.Select(n => File.ReadAllText(Path.Combine(directory, n))).ToArray()); }
            catch (Exception error) { throw new BuildFailedException("Daily task configuration: " + error.Message); }
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64 && report.summary.platform != BuildTarget.StandaloneLinux64) return;
            var destination = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "Config", "DailyTasks");
            Directory.CreateDirectory(destination);
            foreach (var name in ServerTaskConfigurationLoader.FileNames)
                File.Copy(Path.Combine(ServerTaskConfigurationLoader.DefaultDirectory, name), Path.Combine(destination, name), true);
        }
    }
}
