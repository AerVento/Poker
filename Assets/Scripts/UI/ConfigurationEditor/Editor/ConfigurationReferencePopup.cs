using System;
using UnityEditor;
using UnityEngine;

namespace Game.UI
{
    internal sealed class ConfigurationReferencePopup : PopupWindowContent
    {
        private readonly ConfigReferenceKind _kind;
        private readonly string _current;
        private readonly Action<string> _select;
        private string _search = "";
        private Vector2 _scroll;
        private bool _focusSearch = true;
        private int _selected = -1;
        private double _nextRepaint;
        private const float RowHeight = 38;

        internal ConfigurationReferencePopup(ConfigReferenceKind kind, string current, Action<string> select)
        {
            _kind = kind;
            _current = current;
            _select = select;
        }
        public override Vector2 GetWindowSize() => new Vector2(410, 360);
        public override void OnOpen()
        {
            ConfigurationReferenceCatalog.Current.Get(_kind, true);
            EditorApplication.update += Refresh;
        }
        public override void OnClose() => EditorApplication.update -= Refresh;
        private void Refresh()
        {
            if (EditorApplication.timeSinceStartup < _nextRepaint) return;
            _nextRepaint = EditorApplication.timeSinceStartup + 1;
            editorWindow?.Repaint();
        }

        public override void OnGUI(Rect rect)
        {
            var snapshot = ConfigurationReferenceCatalog.Current.Get(_kind);
            var options = snapshot.Search(_search);
            var input = Event.current;
            if (input.type == EventType.KeyDown)
            {
                if (input.keyCode == KeyCode.Escape) { editorWindow.Close(); input.Use(); return; }
                if (input.keyCode == KeyCode.DownArrow || input.keyCode == KeyCode.UpArrow)
                {
                    _selected = Mathf.Clamp(_selected + (input.keyCode == KeyCode.DownArrow ? 1 : -1), 0, options.Length - 1);
                    _scroll.y = Mathf.Max(0, _selected * RowHeight - 100);
                    input.Use();
                }
                else if (input.keyCode == KeyCode.Return || input.keyCode == KeyCode.KeypadEnter)
                {
                    input.Use();
                    if (_selected >= 0 && _selected < options.Length) Choose(options[_selected].Id);
                    else if (!string.IsNullOrWhiteSpace(_search)) Choose(options.Length > 0 ? options[0].Id : _search);
                    return;
                }
            }
            GUILayout.Space(6);
            GUI.SetNextControlName("ReferenceSearch");
            var search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
            if (search != _search) { _search = search; _selected = -1; _scroll = Vector2.zero; options = snapshot.Search(_search); }
            if (_focusSearch) { EditorGUI.FocusTextInControl("ReferenceSearch"); _focusSearch = false; }
            GUILayout.Space(4);
            if (snapshot.Error != null) EditorGUILayout.HelpBox(snapshot.Error, MessageType.Warning);
            using (var scrolling = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scrolling.scrollPosition;
                if (options.Length == 0) EditorGUILayout.LabelField("无匹配项", EditorStyles.centeredGreyMiniLabel);
                for (var index = 0; index < options.Length; index++)
                {
                    var option = options[index];
                    var row = GUILayoutUtility.GetRect(0, RowHeight, GUILayout.ExpandWidth(true));
                    var highlighted = _selected == index || option.Id == _current;
                    if (highlighted) EditorGUI.DrawRect(row, new Color(0.22f, 0.40f, 0.60f, 0.45f));
                    if (GUI.Button(row, new GUIContent("", option.Name + "\n" + option.Id), GUIStyle.none)) Choose(option.Id);
                    GUI.Label(new Rect(row.x + 8, row.y, row.width - 16, 19), option.Name, EditorStyles.label);
                    GUI.Label(new Rect(row.x + 8, row.y + 18, row.width - 16, 18), option.Id, EditorStyles.miniLabel);
                }
            }
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_search)))
                if (GUILayout.Button("使用输入的 ID", GUILayout.Height(24))) Choose(_search);
        }

        private void Choose(string id)
        {
            _select(id);
            editorWindow.Close();
            GUIUtility.ExitGUI();
        }
    }
}
