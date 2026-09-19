using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    public static class DollarPresentationPrefabMigrator
    {
        private const string DollarSpritePath = "Assets/Sprites/UITheme/Dollars-v1.png";
        private const string ShopPrefabPath = "Assets/Prefabs/UI/Panels/ShopPanel/ShopPanel.prefab";
        private const string DailyTaskPrefabPath = "Assets/Prefabs/UI/Panels/DailyTaskPanel/DailyTaskPanel.prefab";

        [MenuItem("Poker/UI/Migrate Dollar Presentation")]
        public static void Migrate()
        {
            ConfigureDollarSprite();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DollarSpritePath);
            if (sprite == null)
                throw new UnityException("Dollar icon could not be imported as a sprite.");

            MigrateShop(sprite);
            MigrateDailyTasks(sprite);
            AssetDatabase.SaveAssets();
            Debug.Log("Dollar presentation prefab migration completed.");
        }

        private static void ConfigureDollarSprite()
        {
            AssetDatabase.ImportAsset(DollarSpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(DollarSpritePath) as TextureImporter;
            if (importer == null)
                throw new UnityException("Missing dollar icon texture importer.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static void MigrateShop(Sprite sprite)
        {
            var root = PrefabUtility.LoadPrefabContents(ShopPrefabPath);
            try
            {
                var panel = root.GetComponent<ShopPanel>();
                if (panel == null) throw new UnityException("ShopPanel component is missing.");
                var serialized = new SerializedObject(panel);
                var balance = serialized.FindProperty("_balance").objectReferenceValue as TextMeshProUGUI;
                var icon = AddIcon(balance, sprite, new Vector2(36, 36), "BalanceCurrencyDisplay");
                serialized.FindProperty("_dollarIcon").objectReferenceValue = icon;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var title = FindNamedText(root.transform, "BalanceTitle");
                if (title != null) title.text = "美金余额";
                PrefabUtility.SaveAsPrefabAsset(root, ShopPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void MigrateDailyTasks(Sprite sprite)
        {
            var root = PrefabUtility.LoadPrefabContents(DailyTaskPrefabPath);
            try
            {
                var panel = root.GetComponent<DailyTaskPanel>();
                if (panel == null) throw new UnityException("DailyTaskPanel component is missing.");
                var serialized = new SerializedObject(panel);
                var balance = serialized.FindProperty("_balance").objectReferenceValue as TextMeshProUGUI;
                var balanceIcon = AddIcon(balance, sprite, new Vector2(34, 34), "BalanceCurrencyDisplay");
                serialized.FindProperty("_dollarIcon").objectReferenceValue = balanceIcon;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                foreach (var card in root.GetComponentsInChildren<DailyTaskCard>(true))
                {
                    var cardSerialized = new SerializedObject(card);
                    UpdateDefaultDailyTaskCopy(cardSerialized);
                    HideLegacyRewardUnit(card.transform);
                    var reward = cardSerialized.FindProperty("_reward").objectReferenceValue as TextMeshProUGUI;
                    var rewardIcon = AddIcon(reward, sprite, new Vector2(34, 34), "RewardCurrencyDisplay");
                    cardSerialized.FindProperty("_rewardCurrencyIcon").objectReferenceValue = rewardIcon;
                    cardSerialized.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, DailyTaskPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void HideLegacyRewardUnit(Transform card)
        {
            foreach (var text in card.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.name == "Unit") text.gameObject.SetActive(false);
            }
        }

        private static void UpdateDefaultDailyTaskCopy(SerializedObject card)
        {
            var taskId = card.FindProperty("_taskId").intValue;
            var title = card.FindProperty("_title").objectReferenceValue as TextMeshProUGUI;
            var description = card.FindProperty("_description").objectReferenceValue as TextMeshProUGUI;
            var reward = card.FindProperty("_reward").objectReferenceValue as TextMeshProUGUI;
            switch (taskId)
            {
                case 1:
                    if (title != null) title.text = "每日补给";
                    if (description != null) description.text = "每日领取一份美金补给。";
                    if (reward != null) reward.text = "+ $1,000";
                    break;
                case 2:
                    if (title != null) title.text = "东山再起";
                    if (description != null) description.text = "总资产不高于 $1,000 时，可领取应急美金。";
                    if (reward != null) reward.text = "+ $2,000";
                    break;
            }
        }

        private static Image AddIcon(TextMeshProUGUI label, Sprite sprite, Vector2 size, string groupName)
        {
            if (label == null) throw new UnityException("Currency label is missing.");
            var group = EnsureLayoutGroup(label, groupName);
            var existing = group.Find("DollarIcon");
            var image = existing == null
                ? new GameObject("DollarIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>()
                : existing.GetComponent<Image>();
            image.transform.SetParent(group, false);
            image.transform.SetSiblingIndex(0);
            label.transform.SetSiblingIndex(1);
            image.gameObject.layer = label.gameObject.layer;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var element = image.GetComponent<LayoutElement>();
            if (element == null) element = image.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = size.x;
            element.minHeight = element.preferredHeight = size.y;
            element.flexibleWidth = element.flexibleHeight = 0;
            label.enableAutoSizing = true;
            label.fontSizeMax = Mathf.Max(label.fontSize, label.fontSizeMax);
            label.fontSizeMin = label.fontSizeMin > 0 ? Mathf.Min(label.fontSizeMin, label.fontSize) : Mathf.Min(20, label.fontSize);
            LayoutRebuilder.ForceRebuildLayoutImmediate(group);
            return image;
        }

        private static RectTransform EnsureLayoutGroup(TextMeshProUGUI label, string groupName)
        {
            if (label.transform.parent.name == groupName)
                return (RectTransform)label.transform.parent;

            var source = label.rectTransform;
            var parent = source.parent;
            var siblingIndex = source.GetSiblingIndex();
            var group = new GameObject(groupName, typeof(RectTransform), typeof(HorizontalLayoutGroup))
                .GetComponent<RectTransform>();
            group.gameObject.layer = label.gameObject.layer;
            group.SetParent(parent, false);
            group.SetSiblingIndex(siblingIndex);
            group.anchorMin = source.anchorMin;
            group.anchorMax = source.anchorMax;
            group.pivot = source.pivot;
            group.anchoredPosition = source.anchoredPosition;
            group.sizeDelta = source.sizeDelta;
            group.localRotation = source.localRotation;
            group.localScale = source.localScale;

            source.SetParent(group, false);
            source.anchorMin = source.anchorMax = source.pivot = new Vector2(0.5f, 0.5f);
            source.anchoredPosition = Vector2.zero;
            source.localRotation = Quaternion.identity;
            source.localScale = Vector3.one;

            var layout = group.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = label.horizontalAlignment == HorizontalAlignmentOptions.Right
                ? TextAnchor.MiddleRight
                : label.horizontalAlignment == HorizontalAlignmentOptions.Left
                    ? TextAnchor.MiddleLeft
                    : TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return group;
        }

        private static TextMeshProUGUI FindNamedText(Transform root, string name)
        {
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.name == name) return text;
            return null;
        }
    }
}
