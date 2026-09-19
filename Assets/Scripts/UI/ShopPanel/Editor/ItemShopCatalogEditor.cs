using System.IO;
using System.Text;
using Framework.Configs;
using Game.Network;
using Game.SO;
using UnityEditor;
using UnityEngine;

namespace Game.UI.Editor
{
    [CustomEditor(typeof(ItemShopCatalogSO))]
    public sealed class ItemShopCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!GUILayout.Button("导出默认配置…")) return;
            var json = ItemCatalogLoader.ExportDefaults((ItemShopCatalogSO)target);
            var path = EditorUtility.SaveFilePanel("导出物品商店默认配置",
                Path.GetDirectoryName(ItemCatalogLoader.ServerPath), ItemCatalogLoader.FileName, "json");
            if (string.IsNullOrEmpty(path)) return;
            ConfigManager.WriteAllTextAtPath(path, json, new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }
    }
}
