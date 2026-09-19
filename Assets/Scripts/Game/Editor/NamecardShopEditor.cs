using System.Collections.Generic;
using System.Text;
using Framework.Configs;
using Framework.UI;
using Game.Network;
using Game.SO;
using Game.UI;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(NamecardProductSO))]
    public sealed class NamecardProductEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_id"), new GUIContent("商品 ID", "售出后请保持不变，用于关联库存。"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_displayName"), new GUIContent("名片名称"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_description"), new GUIContent("商品介绍"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_artwork"), new GUIContent("宽版图片（8:3）"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_artwork4x3"), new GUIContent("紧凑图片（4:3）", "同一 ID 的独立构图，按显示槽比例自动选择；缺失时回退宽版。"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_isListed"), new GUIContent("上架", "取消后保留库存和装备，请勿删除 SO 或目录引用。"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_canPurchase"), new GUIContent("可购买", "取消后仍按上架配置展示；未拥有时不可购买，已拥有时可装备。规则说明填写在商品介绍中。"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_isReward"), new GUIContent("成就奖励", "禁止购买；领取后可手动装备。商品介绍只展示主题文案。"));
            var isDefault = serializedObject.FindProperty("_isDefault");
            EditorGUILayout.PropertyField(isDefault, new GUIContent("默认款", "玩家天然拥有，不需要购买或解锁。目录内只保留一款。"));
            using (new EditorGUI.DisabledScope(isDefault.boolValue))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_price"), new GUIContent("价格（账户筹码）"));
            if (isDefault.boolValue)
                EditorGUILayout.HelpBox("默认款始终免费，未装备其他外观的玩家自动使用它。", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            NamecardArtValidation.DrawPreview((NamecardProductSO)target);
        }
    }

    [CustomEditor(typeof(NamecardCatalogSO))]
    public sealed class NamecardCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("每页 4 款。拖动调整顺序，添加引用以增加商品，下架请在商品 SO 取消上架；默认款放在第一位。", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_products"), new GUIContent("上架商品（按顺序）"), true);
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("导出默认配置…"))
            {
                var path = EditorUtility.SaveFilePanel("导出名片商店默认配置",
                    ShopCatalogConfigManager.DefaultDirectory, NamecardCatalogLoader.FileName, "json");
                if (!string.IsNullOrEmpty(path))
                {
                    ConfigManager.WriteAllTextAtPath(path,
                        NamecardCatalogLoader.ExportDefaults((NamecardCatalogSO)target), new UTF8Encoding(false));
                    AssetDatabase.Refresh();
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class NamecardShopPreview
    {
        private const string PendingKey = "Poker.NamecardShopPreview.Pending";

        static NamecardShopPreview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
                {
                    SessionState.SetBool(PendingKey, false);
                    EditorApplication.delayCall += ShowPreview;
                }
            };
        }

        [MenuItem("Poker/UI/Preview Namecard Shop")]
        public static void OpenPreview()
        {
            if (!EditorApplication.isPlaying)
            {
                SessionState.SetBool(PendingKey, true);
                EditorApplication.EnterPlaymode();
                return;
            }
            ShowPreview();
        }

        public static void ShowPreview()
        {
            if (!EditorApplication.isPlaying || !UIManager.Available)
                return;
            if (UIManager.Instance.ContainsPanel<ShopPanel>())
                UIManager.Instance.PopPanel<ShopPanel>();
            var panel = UIManager.Instance.PushPanel<ShopPanel>();
            panel.SelectTab(ShopTab.Namecards);
            var owned = new HashSet<string> { "galaxy", "neon" };
            uint previewBalance = 500000;
            string equipped = string.Empty;
            System.Action refresh = () =>
            {
                panel.SetAccountView("Poker Player", previewBalance, owned, equipped, true);
                panel.SetFeedback("界面预览 · 示例账户与库存，仅在本次预览中有效");
            };
            panel.PurchaseRequested += product =>
            {
                if (product.IsDefault || owned.Contains(product.Id) || previewBalance < product.Price)
                    return;
                previewBalance -= product.Price;
                owned.Add(product.Id);
                refresh();
            };
            panel.EquipRequested += id =>
            {
                if (!string.IsNullOrEmpty(id) && !owned.Contains(id))
                    return;
                equipped = id;
                refresh();
            };
            refresh();
        }
    }
}
