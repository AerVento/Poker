using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Game.UI
{
    internal sealed class ConfigurationFileTree : TreeView
    {
        private readonly string[] _files;
        private readonly Action<string> _select;
        private readonly Dictionary<int, string> _paths = new Dictionary<int, string>();
        private GUIStyle _fileStyle;

        internal ConfigurationFileTree(TreeViewState state, string[] files, Action<string> select) : base(state)
        {
            _files = files;
            _select = select;
            rowHeight = 22;
            showBorder = false;
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            _paths.Clear();
            var root = new TreeViewItem(0, -1, "Configs");
            var folders = new Dictionary<string, TreeViewItem> { [""] = root };
            var id = 1;
            foreach (var path in _files)
            {
                var parts = path.Split('/');
                var parent = root;
                var folderPath = "";
                for (var i = 0; i < parts.Length - 1; i++)
                {
                    folderPath += parts[i] + "/";
                    if (!folders.TryGetValue(folderPath, out var folder))
                    {
                        folder = new TreeViewItem(id++, i, parts[i]) { icon = EditorGUIUtility.FindTexture("Folder Icon") };
                        parent.AddChild(folder);
                        folders.Add(folderPath, folder);
                    }
                    parent = folder;
                }
                var file = new TreeViewItem(id++, parts.Length - 1, Path.GetFileName(path));
                parent.AddChild(file);
                _paths[file.id] = path;
            }
            if (root.children == null) root.children = new List<TreeViewItem>();
            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (!_paths.TryGetValue(args.item.id, out var path)) { base.RowGUI(args); return; }
            if (_fileStyle == null) _fileStyle = new GUIStyle(EditorStyles.label) { richText = true };
            var rect = args.rowRect;
            rect.xMin += GetContentIndent(args.item);
            var audience = ConfigurationDocument.Classify(path);
            var prefix = audience == ConfigurationDocument.Audience.Client ? "<color=#65C987>[客户端]</color> " :
                audience == ConfigurationDocument.Audience.Server ? "<color=#F07878>[服务端]</color> " :
                audience == ConfigurationDocument.Audience.Both ? "[两端] " : "[未分类] ";
            var name = args.item.displayName.Replace("<", "&lt;").Replace(">", "&gt;");
            GUI.Label(rect, new GUIContent(prefix + name, path), _fileStyle);
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (selectedIds.Count == 1 && _paths.TryGetValue(selectedIds[0], out var path)) _select(path);
        }

        internal void SelectPath(string path)
        {
            foreach (var pair in _paths)
            {
                if (pair.Value != path) continue;
                SetSelection(new[] { pair.Key });
                return;
            }
            SetSelection(Array.Empty<int>());
        }
    }
}
