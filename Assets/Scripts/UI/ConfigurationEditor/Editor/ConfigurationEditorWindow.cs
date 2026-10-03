using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Framework.Configs;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Game.UI
{
    internal sealed class ConfigurationDraftState : ScriptableObject
    {
        public string Json = "";
    }

    public sealed class ConfigurationEditorWindow : EditorWindow
    {
        [SerializeField] private string _relativePath = "";
        [SerializeField] private ConfigurationDraftState _state;
        private string Draft { get => _state == null ? "" : _state.Json; set => _state.Json = value; }
        [SerializeField] private string _savedDraft = "";
        [SerializeField] private string _originalBytes = "";
        [SerializeField] private bool _rawMode;
        [SerializeField] private string _search = "";
        [SerializeField] private int _audienceFilter;
        private JToken _root;
        private string[] _files = Array.Empty<string>();
        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, int> _pages = new Dictionary<string, int>();
        private Vector2 _contentScroll;
        private Vector2 _rawScroll;
        private GUIStyle _rawStyle;
        [SerializeField] private TreeViewState _fileTreeState = new TreeViewState();
        private ConfigurationFileTree _fileTree;
        private string _treeFilter;
        private string _message;
        private MessageType _messageType;
        private double _nextScan;
        private Action _pending;
        private string _newFieldKey;
        private string _newFieldName = "";
        private const int PageSize = 25;
        private static readonly string[] Filters = { "全部", "仅客户端", "仅服务器", "两端共用", "未分类" };
        private static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Configs"));
        private string FilePath => Path.Combine(DirectoryPath, _relativePath);
        private bool Dirty => Draft != _savedDraft;

        [MenuItem("Poker/配置编辑器")]
        public static void Open()
        {
            GetWindow<ConfigurationEditorWindow>("配置编辑器");
        }

        private void OnEnable()
        {
            if (_state == null)
            {
                _state = CreateInstance<ConfigurationDraftState>();
                _state.hideFlags = HideFlags.HideAndDontSave;
            }
            minSize = new Vector2(760, 480);
            saveChangesMessage = "配置有未保存的修改。";
            RestoreTree();
            Scan();
            EditorApplication.update += UpdateFiles;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateFiles;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnDestroy()
        {
            if (_state != null) DestroyImmediate(_state);
        }

        private void UpdateFiles()
        {
            if (EditorApplication.timeSinceStartup < _nextScan) return;
            _nextScan = EditorApplication.timeSinceStartup + 2;
            Scan();
            Repaint();
        }

        private void Scan()
        {
            try
            {
                var files = ConfigurationDocument.Discover(DirectoryPath).Union(ConfigurationMetadata.Types.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
                if (!_files.SequenceEqual(files)) { _files = files; Repaint(); }
            }
            catch (Exception exception) { ShowError(exception); }
        }

        private void OnUndoRedo()
        {
            RestoreTree();
            _foldouts.Clear();
            _pages.Clear();
            Repaint();
        }

        private void RestoreTree()
        {
            _root = null;
            if (!string.IsNullOrEmpty(Draft))
            {
                try { _root = ConfigurationDocument.Parse(Draft); if (!_rawMode) PrepareForm(); }
                catch (Exception exception) { _rawMode = true; ShowError(exception); }
            }
            hasUnsavedChanges = Dirty;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Configs", EditorStyles.boldLabel, GUILayout.Width(65));
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));
                _audienceFilter = EditorGUILayout.Popup(_audienceFilter, Filters, EditorStyles.toolbarPopup, GUILayout.Width(105));
                if (GUILayout.Button(new GUIContent("刷新", "重新扫描 Configs"), EditorStyles.toolbarButton, GUILayout.Width(45))) Scan();
                if (GUILayout.Button("校验全部配置", EditorStyles.toolbarButton, GUILayout.Width(100))) _pending = ValidateAll;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawFiles();
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawDocument();
                }
            }
            if (_pending != null)
            {
                var action = _pending;
                _pending = null;
                action();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawFiles()
        {
            using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(310));
            var files = _files.Where(file => MatchesFilter(ConfigurationDocument.Classify(file))).ToArray();
            var filter = _audienceFilter + "|" + string.Join("|", files);
            if (_fileTree == null || _treeFilter != filter)
            {
                var first = _fileTree == null && _fileTreeState.expandedIDs.Count == 0;
                _fileTree = new ConfigurationFileTree(_fileTreeState, files, path =>
                    _pending = () => { if (path != _relativePath) SelectFile(path); _fileTree.SelectPath(_relativePath); });
                if (first) _fileTree.ExpandAll();
                _fileTree.SelectPath(_relativePath);
                _treeFilter = filter;
            }
            _fileTree.searchString = _search;
            _fileTree.OnGUI(GUILayoutUtility.GetRect(0, 100000, 0, 100000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)));
        }

        private bool MatchesFilter(ConfigurationDocument.Audience audience)
        {
            return _audienceFilter == 0 || (_audienceFilter == 1 && audience == ConfigurationDocument.Audience.Client)
                || (_audienceFilter == 2 && audience == ConfigurationDocument.Audience.Server)
                || (_audienceFilter == 3 && audience == ConfigurationDocument.Audience.Both)
                || (_audienceFilter == 4 && audience == ConfigurationDocument.Audience.Unknown);
        }

        private void DrawDocument()
        {
            if (string.IsNullOrEmpty(_relativePath))
            {
                EditorGUILayout.HelpBox("选择一个配置文件", MessageType.Info);
                return;
            }
            EditorGUILayout.SelectableLabel(_relativePath, EditorStyles.boldLabel, GUILayout.Height(22));
            var audience = ConfigurationDocument.Classify(_relativePath);
            EditorGUILayout.LabelField("读取端", ConfigurationDocument.AudienceLabel(audience));
            if (audience == ConfigurationDocument.Audience.Unknown)
                EditorGUILayout.HelpBox("此文件尚未登记读取端。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(!Dirty))
                    if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(50))) _pending = () => SaveFile();
                if (GUILayout.Button("重新读取", EditorStyles.toolbarButton, GUILayout.Width(70))) _pending = Reload;
                GUILayout.FlexibleSpace();
                var mode = GUILayout.Toolbar(_rawMode ? 1 : 0, new[] { "表单", "JSON" }, EditorStyles.toolbarButton, GUILayout.Width(100));
                if ((mode == 1) != _rawMode) _pending = () => ChangeMode(mode == 1);
            }
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, _messageType);
            if (_rawMode && !string.IsNullOrEmpty(Draft))
            {
                DrawRawJson();
                EditorGUILayout.LabelField(Dirty ? "未保存" : "已保存 · 修改后重启对应程序生效", EditorStyles.miniLabel);
                return;
            }
            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            if (!File.Exists(FilePath) && ConfigurationMetadata.Find(_relativePath) != null && _originalBytes.Length == 0)
            {
                EditorGUILayout.HelpBox("配置文件尚未创建", MessageType.Info);
                if (GUILayout.Button("创建默认配置", GUILayout.Width(140)))
                    _pending = () =>
                    {
                        try { ConfigManager.CreateFile(ConfigurationMetadata.Find(_relativePath)); LoadFile(_relativePath); Scan(); }
                        catch (Exception exception) { ShowError(exception); }
                    };
            }
            else if (_root != null)
            {
                var previousWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = Mathf.Clamp((position.width - 290) * 0.38f, 150, 290);
                DrawNode(_root, "根节点", "$", 0, ConfigurationMetadata.Find(_relativePath));
                EditorGUIUtility.labelWidth = previousWidth;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField(Dirty ? "未保存" : "已保存 · 修改后重启对应程序生效", EditorStyles.miniLabel);
        }

        private void DrawNode(JToken token, string label, string key, int depth, Type type = null, JsonProperty member = null, Action remove = null)
        {
            var header = ConfigurationMetadata.Attribute<HeaderAttribute>(member);
            var space = ConfigurationMetadata.Attribute<SpaceAttribute>(member);
            if (space != null) EditorGUILayout.Space(space.height);
            if (header != null) EditorGUILayout.LabelField(header.header, EditorStyles.boldLabel);
            // Deep JSON remains editable in the JSON view without exhausting the GUI stack.
            if (depth > 20)
            {
                EditorGUILayout.LabelField(label, "请在 JSON 视图编辑更深层内容");
                return;
            }
            if (token is JObject obj)
            {
                var dictionaryType = type != null && ConfigManager.GetContract(type) is JsonDictionaryContract dictionary ? dictionary.DictionaryValueType : null;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var open = Foldout(key, label, depth == 0, ConfigurationMetadata.Attribute<TooltipAttribute>(member)?.tooltip);
                    if ((type == null || dictionaryType != null) && IconButton("Toolbar Plus", "新增字段"))
                    {
                        _newFieldKey = key;
                        _newFieldName = "";
                        _foldouts[key] = true;
                    }
                    DrawRemove(remove);
                    if (!open) return;
                }
                using (new EditorGUI.IndentLevelScope())
                {
                    var members = ConfigurationMetadata.Members(type).ToArray();
                    foreach (var definition in members)
                    {
                        var property = obj.Property(definition.PropertyName);
                        if (property != null)
                            DrawNode(property.Value, property.Name, key + "/" + Escape(property.Name), depth + 1, definition.PropertyType, definition,
                                ConfigurationMetadata.IsRequired(definition) ? null : (Action)(() => Mutate(property.Remove)));
                        else
                        {
                            using var missing = new EditorGUILayout.HorizontalScope();
                            EditorGUILayout.LabelField(new GUIContent(ObjectNames.NicifyVariableName(definition.PropertyName),
                                ConfigurationMetadata.Attribute<TooltipAttribute>(definition)?.tooltip), new GUIContent("未设置"));
                            if (GUILayout.Button("添加", EditorStyles.miniButton, GUILayout.Width(50)))
                                _pending = () =>
                                {
                                    try { Mutate(() => obj.Add(definition.PropertyName, ConfigurationMetadata.MemberDefault(type, definition))); }
                                    catch (Exception exception) { ShowError(exception); }
                                };
                        }
                    }
                    foreach (var property in obj.Properties().Where(p => !members.Any(m => m.PropertyName == p.Name)).ToArray())
                        DrawNode(property.Value, property.Name, key + "/" + Escape(property.Name), depth + 1, dictionaryType);
                    if (!obj.HasValues) EditorGUILayout.LabelField("空对象", EditorStyles.miniLabel);
                    if (_newFieldKey == key)
                    {
                        using var row = new EditorGUILayout.HorizontalScope();
                        _newFieldName = EditorGUILayout.TextField("字段名", _newFieldName);
                        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newFieldName) || obj.Property(_newFieldName) != null))
                        {
                            if (GUILayout.Button(dictionaryType == null ? "选择类型" : "添加", EditorStyles.miniButton, GUILayout.Width(75)))
                            {
                                var name = _newFieldName;
                                if (dictionaryType != null)
                                    _pending = () =>
                                    {
                                        try { Mutate(() => obj.Add(name, ConfigurationMetadata.NewValue(dictionaryType))); _newFieldKey = null; }
                                        catch (Exception exception) { ShowError(exception); }
                                    };
                                else ShowTypes(value =>
                                {
                                    Mutate(() => obj.Add(name, value), false);
                                    _newFieldKey = null;
                                });
                            }
                        }
                        if (IconButton("winbtn_win_close", "取消新增")) _newFieldKey = null;
                    }
                }
            }
            else if (token is JArray array)
            {
                DrawArray(array, label, key, depth, type, ConfigurationMetadata.Attribute<TooltipAttribute>(member)?.tooltip, remove);
            }
            else
            {
                using var fieldRow = new EditorGUILayout.HorizontalScope();
                using (new EditorGUILayout.VerticalScope()) DrawScalar(token, label, type, member);
                DrawRemove(remove);
            }
        }

        private void DrawScalar(JToken token, string label, Type type, JsonProperty member)
        {
            var reference = ConfigurationMetadata.Attribute<ConfigReferenceAttribute>(member);
            if (reference != null && type == typeof(string) && token.Type == JTokenType.String)
            {
                DrawReference(token, label, member, reference.Kind);
                return;
            }
            EditorGUI.BeginChangeCheck();
            JToken replacement = null;
            var content = new GUIContent(ObjectNames.NicifyVariableName(label), ConfigurationMetadata.Attribute<TooltipAttribute>(member)?.tooltip ?? label);
            var scalarType = type == null ? null : Nullable.GetUnderlyingType(type) ?? type;
            if (scalarType != null && scalarType.IsEnum && token.Type != JTokenType.Null)
            {
                var names = Enum.GetNames(scalarType);
                string current = null;
                if (token.Type == JTokenType.String) current = token.Value<string>();
                else if (Enum.TryParse(scalarType, token.ToString(), out var parsedEnum)) current = Enum.GetName(scalarType, parsedEnum);
                var selected = Array.IndexOf(names, current);
                var choices = selected < 0 ? new[] { "无效值：" + token }.Concat(names).ToArray() : names;
                var choice = EditorGUILayout.Popup(content, selected < 0 ? 0 : selected, choices.Select(n => new GUIContent(n)).ToArray());
                if (selected >= 0 || choice > 0)
                {
                    var value = Enum.Parse(scalarType, choices[choice]);
                    replacement = token.Type == JTokenType.String ? new JValue(value.ToString()) :
                        new JRaw(Convert.ToString(Convert.ChangeType(value, Enum.GetUnderlyingType(scalarType)), System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            else if (token.Type == JTokenType.Boolean)
                replacement = new JValue(EditorGUILayout.Toggle(content, token.Value<bool>()));
            else if (token.Type == JTokenType.String)
            {
                var value = token.Value<string>();
                if (label.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
                    replacement = new JValue(EditorGUILayout.PasswordField(content, value));
                else if (value.Contains("\n"))
                {
                    EditorGUILayout.LabelField(content);
                    replacement = new JValue(EditorGUILayout.TextArea(value, GUILayout.MinHeight(55)));
                }
                else replacement = new JValue(EditorGUILayout.TextField(content, value));
            }
            else if (token.Type == JTokenType.Raw || token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                // A delayed numeric text field preserves large integers and decimal literals exactly.
                var value = EditorGUILayout.DelayedTextField(content, ConfigurationDocument.Serialize(token));
                if (EditorGUI.EndChangeCheck())
                {
                    try
                    {
                        replacement = ConfigurationDocument.Parse(value);
                        if (replacement.Type != JTokenType.Raw) throw new InvalidDataException("请输入有效的 JSON 数字。");
                        if (scalarType != null) ConfigManager.Deserialize(value, scalarType);
                        QueueReplace(token, replacement);
                    }
                    catch (Exception exception) { ShowError(exception); }
                }
                return;
            }
            else
            {
                using var row = new EditorGUILayout.HorizontalScope();
                EditorGUILayout.LabelField(content, new GUIContent("null"));
                if (GUILayout.Button(type == null ? "设置类型" : "创建默认值", EditorStyles.miniButton, GUILayout.Width(85)))
                {
                    if (type == null) ShowTypes(value => QueueReplace(token, value));
                    else
                    {
                        try { QueueReplace(token, ConfigurationMetadata.NewValue(Nullable.GetUnderlyingType(type) ?? type)); }
                        catch (Exception exception) { ShowError(exception); }
                    }
                }
            }
            if (EditorGUI.EndChangeCheck() && replacement != null) QueueReplace(token, replacement);
        }

        private void DrawReference(JToken token, string label, JsonProperty member, ConfigReferenceKind kind)
        {
            var value = token.Value<string>();
            var snapshot = ConfigurationReferenceCatalog.Current.Get(kind);
            var content = new GUIContent(ObjectNames.NicifyVariableName(label), ConfigurationMetadata.Attribute<TooltipAttribute>(member)?.tooltip);
            var rect = EditorGUILayout.GetControlRect();
            var field = EditorGUI.PrefixLabel(rect, content);
            var style = new GUIStyle(EditorStyles.popup);
            if (snapshot.Error == null && !snapshot.Contains(value))
                style.normal.textColor = style.hover.textColor = style.focused.textColor = style.active.textColor = new Color(1f, 0.3f, 0.3f);
            var display = new GUIContent(string.IsNullOrEmpty(value) ? "选择 ID" : value, snapshot.Error ?? value);
            if (!EditorGUI.DropdownButton(field, display, FocusType.Keyboard, style)) return;
            PopupWindow.Show(field, new ConfigurationReferencePopup(kind, value, id =>
            {
                if (this == null || token.Root != _root) return;
                Mutate(() => token.Replace(new JValue(id)), false);
                Repaint();
            }));
        }

        private void DrawArray(JArray array, string label, string key, int depth, Type type, string tooltip, Action remove)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var open = Foldout(key, label + "  [" + array.Count + "]", depth == 0, tooltip);
                if (IconButton("Toolbar Plus", "添加元素"))
                {
                    var elementType = ConfigurationMetadata.TaskParameterType(array, array.Count) ?? ConfigurationMetadata.ElementType(type);
                    if (elementType != null && !typeof(JToken).IsAssignableFrom(elementType))
                        _pending = () =>
                        {
                            try { Mutate(() => array.Add(ConfigurationMetadata.NewValue(elementType))); _foldouts[key] = true; _foldouts[key + "/" + (array.Count - 1)] = true; _pages[key] = (array.Count - 1) / PageSize; }
                            catch (Exception exception) { ShowError(exception); }
                        };
                    else ShowTypes(value => Mutate(() => array.Add(value)));
                }
                DrawRemove(remove);
                if (!open) return;
            }
            using var indent = new EditorGUI.IndentLevelScope();
            // GUILayout containers do not honor EditorGUI.indentLevel; indent their bounds explicitly.
            using var indentedRow = new EditorGUILayout.HorizontalScope();
            GUILayout.Space(EditorGUI.indentLevel * 15f);
            using var body = new EditorGUILayout.VerticalScope();
            using var localIndent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
            if (array.Count == 0) EditorGUILayout.LabelField("空数组", EditorStyles.miniLabel);
            _pages.TryGetValue(key, out var page);
            var pages = Math.Max(1, (array.Count + PageSize - 1) / PageSize);
            page = Mathf.Clamp(page, 0, pages - 1);
            _pages[key] = page;
            if (pages > 1)
            {
                using var paging = new EditorGUILayout.HorizontalScope();
                using (new EditorGUI.DisabledScope(page == 0))
                    if (IconButton("Animation.PrevKey", "上一页")) _pages[key] = page - 1;
                GUILayout.Label((page + 1) + " / " + pages, GUILayout.Width(70));
                using (new EditorGUI.DisabledScope(page == pages - 1))
                    if (IconButton("Animation.NextKey", "下一页")) _pages[key] = page + 1;
                GUILayout.FlexibleSpace();
            }
            for (var index = page * PageSize; index < Math.Min(array.Count, (page + 1) * PageSize); index++)
            {
                var slot = index;
                var child = array[index];
                using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(ElementLabel(child, index), EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(index == 0))
                        if (IconButton("d_scrollup", "上移")) _pending = () => Mutate(() => Move(array, slot, slot - 1));
                    using (new EditorGUI.DisabledScope(index == array.Count - 1))
                        if (IconButton("d_scrolldown", "下移")) _pending = () => Mutate(() => Move(array, slot, slot + 1));
                    if (IconButton("TreeEditor.Duplicate", "复制元素")) _pending = () => Mutate(() => array.Insert(slot + 1, child.DeepClone()));
                    if (IconButton("winbtn_win_close", "移除元素")) _pending = () => Mutate(() => array.RemoveAt(slot));
                }
                DrawNode(child, "Element " + index, key + "/" + index, depth + 1, ConfigurationMetadata.TaskParameterType(array, index) ?? ConfigurationMetadata.ElementType(type));
            }
        }

        private static string ElementLabel(JToken token, int index)
        {
            if (token is JObject obj)
            {
                foreach (var name in new[] { "name", "id", "item-id", "task-id" })
                    if (obj[name]?.Type == JTokenType.String) return "[" + index + "]  " + obj[name].Value<string>();
            }
            return "[" + index + "]";
        }

        private static void Move(JArray array, int from, int to)
        {
            var token = array[from];
            array.RemoveAt(from);
            array.Insert(to, token);
        }

        private bool Foldout(string key, string label, bool defaultValue, string tooltip = null)
        {
            if (!_foldouts.TryGetValue(key, out var expanded)) expanded = defaultValue;
            expanded = EditorGUILayout.Foldout(expanded, new GUIContent(label, tooltip ?? label), true);
            _foldouts[key] = expanded;
            return expanded;
        }

        private static string Escape(string key) => key.Replace("~", "~0").Replace("/", "~1");

        private static bool IconButton(string icon, string tooltip)
        {
            var content = new GUIContent(EditorGUIUtility.IconContent(icon)) { tooltip = tooltip };
            return GUILayout.Button(content, EditorStyles.miniButton, GUILayout.Width(24), GUILayout.Height(19));
        }

        private void ShowTypes(Action<JToken> append, Action clone = null)
        {
            var menu = new GenericMenu();
            if (clone != null) menu.AddItem(new GUIContent("复制末尾元素"), false, () => { clone(); Repaint(); });
            foreach (var entry in new[] { "对象", "数组", "字符串", "数字", "布尔值", "null" })
            {
                var type = entry;
                menu.AddItem(new GUIContent(type), false, () =>
                {
                    JToken value = type == "对象" ? (JToken)new JObject() : type == "数组" ? new JArray() :
                        type == "字符串" ? new JValue("") : type == "数字" ? new JRaw("0") :
                        type == "布尔值" ? new JValue(false) : JValue.CreateNull();
                    append(value);
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }

        private void PrepareForm()
        {
            try
            {
                if (_root != null && ConfigurationMetadata.FillRequired(_root, ConfigurationMetadata.Find(_relativePath)))
                {
                    SetDraft(ConfigurationDocument.Serialize(_root, true));
                    _message = "已补齐必填字段，请保存。";
                    _messageType = MessageType.Info;
                }
            }
            catch (Exception exception) { ShowError(exception); }
        }

        private void DrawRemove(Action remove)
        {
            if (remove != null && IconButton("winbtn_win_close", "移除可选字段")) _pending = remove;
        }

        private void DrawRawJson()
        {
            _rawStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = false };
            var viewport = GUILayoutUtility.GetRect(0, 100000, 0, 100000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var size = _rawStyle.CalcSize(new GUIContent(Draft + "\n"));
            var content = new Rect(0, 0, Mathf.Max(viewport.width - 18, size.x + 8), Mathf.Max(viewport.height - 18, size.y + 8));
            // Handle the wheel before TextArea can consume it for its own caret scrolling.
            if (Event.current.type == EventType.ScrollWheel && viewport.Contains(Event.current.mousePosition))
            {
                _rawScroll.y = Mathf.Clamp(_rawScroll.y + Event.current.delta.y * 20, 0, Mathf.Max(0, content.height - viewport.height + 18));
                Event.current.Use();
                Repaint();
            }
            _rawScroll = GUI.BeginScrollView(viewport, _rawScroll, content);
            var edited = EditorGUI.TextArea(content, Draft, _rawStyle);
            GUI.EndScrollView();
            if (edited != Draft) SetDraft(edited);
        }

        private void QueueReplace(JToken token, JToken replacement)
        {
            _pending = () => Mutate(() =>
            {
                if (token.Parent == null) _root = replacement;
                else token.Replace(replacement);
            }, false);
        }

        private void Mutate(Action action, bool structureChanged = true)
        {
            action();
            ConfigurationMetadata.FillRequired(_root, ConfigurationMetadata.Find(_relativePath));
            SetDraft(ConfigurationDocument.Serialize(_root, true));
            if (structureChanged) _newFieldKey = null;
        }

        private void SetDraft(string value)
        {
            if (Draft == value) return;
            Undo.RecordObject(_state, "编辑配置");
            Draft = value;
            hasUnsavedChanges = Dirty;
            _message = null;
        }

        private void SelectFile(string relativePath)
        {
            if (!ResolveUnsaved()) return;
            LoadFile(relativePath);
        }

        private void LoadFile(string relativePath)
        {
            try
            {
                if (!File.Exists(Path.Combine(DirectoryPath, relativePath)) && ConfigurationMetadata.Find(relativePath) != null)
                {
                    Undo.ClearUndo(_state);
                    _relativePath = relativePath;
                    _originalBytes = "";
                    Draft = _savedDraft = "";
                    _root = null;
                    _message = null;
                    hasUnsavedChanges = false;
                    return;
                }
                var bytes = File.ReadAllBytes(Path.Combine(DirectoryPath, relativePath));
                var text = ConfigurationDocument.Decode(bytes);
                Undo.ClearUndo(_state);
                _relativePath = relativePath;
                _originalBytes = Convert.ToBase64String(bytes);
                Draft = text;
                _message = null;
                _rawMode = false;
                try { Draft = ConfigurationDocument.Serialize(ConfigurationDocument.Parse(text), true); }
                catch (Exception exception) { _rawMode = true; ShowError(exception); }
                _savedDraft = Draft;
                RestoreTree();
                _foldouts.Clear();
                _pages.Clear();
                _contentScroll = Vector2.zero;
                _rawScroll = Vector2.zero;
                GUI.FocusControl(null);
            }
            catch (Exception exception) { ShowError(exception); }
        }

        private bool ResolveUnsaved()
        {
            if (!Dirty) return true;
            var choice = EditorUtility.DisplayDialogComplex("未保存的配置", _relativePath, "保存", "取消", "放弃修改");
            return choice == 0 ? SaveFile() : choice == 2;
        }

        private void Reload()
        {
            if (Dirty && !EditorUtility.DisplayDialog("重新读取", "放弃当前修改并读取磁盘文件？", "重新读取", "取消")) return;
            LoadFile(_relativePath);
        }

        private void ChangeMode(bool raw)
        {
            if (!raw)
            {
                try { _root = ConfigurationDocument.Parse(Draft); }
                catch (Exception exception) { ShowError(exception); return; }
            }
            _rawMode = raw;
            _message = null;
            if (!raw) PrepareForm();
            _contentScroll = Vector2.zero;
            _rawScroll = Vector2.zero;
        }

        private bool SaveFile()
        {
            try
            {
                ConfigurationDocument.Save(FilePath, Draft, Convert.FromBase64String(_originalBytes));
                _originalBytes = Convert.ToBase64String(File.ReadAllBytes(FilePath));
                _savedDraft = Draft;
                hasUnsavedChanges = false;
                _message = "已保存。修改后重启对应程序生效。";
                _messageType = MessageType.Info;
                return true;
            }
            catch (Exception exception) { ShowError(exception); return false; }
        }

        public override void SaveChanges()
        {
            if (SaveFile()) base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            Draft = _savedDraft;
            RestoreTree();
            base.DiscardChanges();
        }

        private void ValidateAll()
        {
            if (Dirty)
            {
                EditorUtility.DisplayDialog("尚有未保存的修改", "请先保存当前配置，再校验全部文件。", "确定");
                return;
            }
            try
            {
                var errors = ConfigurationDocument.ValidateAll(DirectoryPath);
                EditorUtility.DisplayDialog(errors.Count == 0 ? "校验通过" : "校验未通过",
                    errors.Count == 0 ? "客户端与服务端配置全部校验通过。" : string.Join("\n\n", errors), "确定");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("校验未通过", exception.Message, "确定");
            }
        }

        private void ShowError(Exception exception)
        {
            _message = exception.Message;
            _messageType = MessageType.Error;
        }
    }
}
