using System;
using System.IO;
using System.Linq;
using Framework.UI;
using Game.Achievements;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    public static class AchievementUIEditor
    {
        private const string PanelDirectory = "Assets/Prefabs/UI/Panels/";
        private static TMP_FontAsset _font;
        private static readonly Color Gold = new Color(0.87f, 0.71f, 0.43f);
        private static readonly Color Ink = new Color(0.045f, 0.095f, 0.08f);
        private static readonly Color White = new Color(0.91f, 0.94f, 0.89f);

        [MenuItem("Poker/UI/Build Achievement UI")]
        public static void Build()
        {
            _font = AssetDatabase.LoadAssetAtPath<GameObject>(PanelDirectory + "TitlePanel/TitlePanel.prefab")
                .GetComponentInChildren<TextMeshProUGUI>(true).font;
            RequireIcon("Default");
            RequireIcon("FullHouse");
            BuildRow();
            BuildPanel();
            BuildBanner();
            RegisterPanels();
            AddTitleEntry();
            AssetDatabase.SaveAssets();
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image Image(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string text, float x, float y, float width, float height,
            float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font; label.text = text; label.fontSize = size; label.color = color;
            label.alignment = alignment; label.raycastTarget = false; label.richText = false;
            label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private static Button Button(string name, Transform parent, string text, float x, float y, float width, float height, bool primary = false)
        {
            var image = Image(name, parent, x, y, width, height, primary ? Gold : new Color(0.13f, 0.24f, 0.20f));
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(0.55f, 0.57f, 0.54f, 0.6f);
            button.colors = colors;
            Label("Label", button.transform, text, 8, 0, width - 16, height, 23, primary ? Ink : White, TextAlignmentOptions.Center);
            return button;
        }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyRoundedStyle(GameObject root)
        {
            var surface = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UITheme/Surface.png");
            var outline = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UITheme/Outline.png");
            if (surface == null || outline == null)
                throw new InvalidOperationException("The shared UI theme surface and outline are required.");

            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                bool border = image.name == "Accent" || image.name == "GoldLine";
                bool button = image.GetComponent<Button>() != null;
                bool surfaceElement = image.name == "AchievementRow" || image.name == "IconFrame" || image.name == "Banner";
                if (!border && !button && !surfaceElement) continue;
                image.sprite = border ? outline : surface;
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = image.name == "DescriptionToggle" ? 2f : 1f;
                // Rounded visuals retain the full rectangular button hit area.
                image.raycastTarget = button;
                if (border)
                {
                    Stretch(image.rectTransform);
                    image.transform.SetAsFirstSibling();
                }
            }
        }

        [MenuItem("Poker/UI/Update Achievement Rounded Style")]
        public static void UpdateRoundedStyle()
        {
            foreach (var name in new[] { "AchievementPanel", "AchievementRow", "AchievementBannerPanel" })
            {
                var path = PanelDirectory + "AchievementPanel/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ApplyRoundedStyle(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void Save(GameObject root, string name)
        {
            ApplyRoundedStyle(root);
            PrefabUtility.SaveAsPrefabAsset(root, PanelDirectory + "AchievementPanel/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void BuildRow()
        {
            var root = Rect("AchievementRow", null, 0, 0, 1640, 150).gameObject;
            var row = root.AddComponent<AchievementRow>();
            root.AddComponent<Image>().color = new Color(0.075f, 0.145f, 0.125f);
            root.GetComponent<Image>().raycastTarget = false;
            root.AddComponent<LayoutElement>().preferredHeight = 150;
            Set(row, "_accent", Image("Accent", root.transform, 0, 0, 4, 150, Gold));
            Image("IconFrame", root.transform, 24, 24, 100, 100, new Color(0.14f, 0.19f, 0.19f));
            var icon = Image("Icon", root.transform, 28, 28, 92, 92, Color.white);
            icon.sprite = Resources.Load<Sprite>("UI/Achievements/FullHouse"); icon.preserveAspect = true;
            Set(row, "_icon", icon);
            Set(row, "_name", Label("Name", root.transform, "葫芦之上", 150, 17, 780, 43, 30, White));
            Set(row, "_category", Label("Category", root.transform, "特殊", 960, 24, 130, 30, 19, Gold, TextAlignmentOptions.Right));
            Set(row, "_description", Label("Description", root.transform, "摊牌时，用葫芦击败同一奖池中另一位持有葫芦的玩家。", 150, 61, 960, 50, 22, new Color(0.64f, 0.76f, 0.69f)));
            Image("ProgressTrack", root.transform, 150, 126, 510, 4, new Color(0.18f, 0.28f, 0.23f));
            var fill = Image("ProgressFill", root.transform, 150, 126, 510, 4, Gold);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillAmount = 0;
            Set(row, "_progressFill", fill);
            Set(row, "_progress", Label("Progress", root.transform, "0 / 1", 680, 111, 230, 28, 19, Gold));
            Set(row, "_reward", Label("Reward", root.transform, "$1,000 筹码", 1140, 24, 220, 100, 24, Gold, TextAlignmentOptions.Center));
            var claim = Button("Claim", root.transform, "未达成", 1400, 48, 194, 54, true);
            Set(row, "_claim", claim); Set(row, "_claimLabel", claim.GetComponentInChildren<TextMeshProUGUI>());
            ConfigureRowDescription(root);
            Save(root, "AchievementRow");
        }

        private static ScrollRect Scroll(string name, Transform parent, float x, float y, float width, float height, bool horizontal)
        {
            var root = Rect(name, parent, x, y, width, height);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var viewport = Rect("Viewport", root, 0, 0, width - (horizontal ? 0 : 16), height);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 0, width - (horizontal ? 0 : 16), 0);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            if (horizontal)
            {
                content.sizeDelta = new Vector2(0, height);
                var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 14; layout.childControlWidth = false; layout.childControlHeight = false;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 14; layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var bar = Image("Scrollbar", root, width - 6, 0, 6, height, new Color(0.10f, 0.20f, 0.16f));
                var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
                var handle = Image("Handle", bar.transform, 0, 0, 6, height, Gold);
                Stretch(handle.rectTransform); handle.raycastTarget = true;
                scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scroll.verticalScrollbar = scrollbar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = horizontal; scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 42;
            return scroll;
        }

        private static void BuildPanel()
        {
            var root = Rect("AchievementPanel", null, 0, 0, 0, 0).gameObject;
            Stretch((RectTransform)root.transform);
            root.AddComponent<Image>().color = Ink;
            var panel = root.AddComponent<AchievementPanel>();
            var layout = Rect("Layout", root.transform, 0, 0, 1740, 940);
            layout.anchorMin = layout.anchorMax = layout.pivot = new Vector2(0.5f, 0.5f);
            Set(panel, "_layout", layout);
            Label("Eyebrow", layout, "POKER  /  ACHIEVEMENTS", 50, 16, 800, 30, 18, Gold);
            Label("Title", layout, "成就", 50, 55, 800, 65, 48, White);
            Label("Subtitle", layout, "记录每一次值得纪念的手牌", 185, 73, 800, 40, 24, new Color(0.61f, 0.74f, 0.66f));
            Set(panel, "_summary", Label("Summary", layout, "已达成  0 / 1", 1240, 69, 440, 40, 26, Gold, TextAlignmentOptions.Right));
            var tabs = Scroll("CategoryTabs", layout, 50, 151, 1640, 54, true);
            Set(panel, "_tabs", tabs.content);
            var template = Button("TabTemplate", tabs.content, "全部", 0, 0, 184, 50);
            template.gameObject.SetActive(false); Set(panel, "_tabTemplate", template);
            var scroll = Scroll("Vertical Scroll View", layout, 50, 233, 1656, 594, false);
            Set(panel, "_scroll", scroll);
            Set(panel, "_rowPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PanelDirectory + "AchievementPanel/AchievementRow.prefab").GetComponent<AchievementRow>());
            Set(panel, "_empty", Label("Empty", layout, "暂无成就数据", 150, 445, 1440, 80, 25, new Color(0.61f, 0.72f, 0.65f), TextAlignmentOptions.Center));
            Set(panel, "_back", Button("Back", layout, "返回", 50, 863, 140, 52));
            Set(panel, "_refresh", Button("Refresh", layout, "刷新", 1530, 863, 160, 52));
            Set(panel, "_feedback", Label("Feedback", layout, "每个成就仅可完成一次，奖励在大厅领取。", 225, 865, 1230, 50, 21, new Color(0.63f, 0.74f, 0.67f)));
            Save(root, "AchievementPanel");
        }

        private static void BuildBanner()
        {
            var root = Rect("AchievementBannerPanel", null, 0, 0, 0, 0).gameObject;
            Stretch((RectTransform)root.transform);
            var panel = root.AddComponent<AchievementBannerPanel>();
            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0; group.interactable = group.blocksRaycasts = false;
            Set(panel, "_group", group);
            var banner = Rect("Banner", root.transform, 0, 24, 880, 132);
            banner.anchorMin = banner.anchorMax = banner.pivot = new Vector2(0.5f, 1);
            Set(panel, "_banner", banner);
            banner.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.13f, 0.12f, 0.98f);
            banner.GetComponent<Image>().raycastTarget = false;
            Image("GoldLine", banner, 0, 0, 880, 3, Gold);
            Set(panel, "_icon", Image("Icon", banner, 22, 19, 94, 94, Color.white));
            Label("Unlocked", banner, "成就达成", 140, 10, 690, 29, 18, Gold);
            Set(panel, "_name", Label("Name", banner, "葫芦之上", 140, 39, 710, 40, 30, White));
            Set(panel, "_description", Label("Description", banner, "", 140, 80, 710, 42, 20, new Color(0.69f, 0.79f, 0.72f)));
            ConfigureBannerDescription(root);
            Save(root, "AchievementBannerPanel");
        }

        // Also used by the full builder so future rebuilds retain the expandable row.
        private static void ConfigureRowDescription(GameObject root)
        {
            var row = root.GetComponent<AchievementRow>();
            var description = root.transform.Find("Description").GetComponent<TextMeshProUGUI>();
            description.overflowMode = TextOverflowModes.Ellipsis;
            description.enableWordWrapping = true;
            var toggleTransform = root.transform.Find("DescriptionToggle");
            var toggle = toggleTransform != null ? toggleTransform.GetComponent<Button>() :
                Button("DescriptionToggle", root.transform, "详情 +", 960, 111, 140, 28);
            var label = toggle.GetComponentInChildren<TextMeshProUGUI>(true);
            label.fontSize = 18;
            label.color = Gold;
            Set(row, "_descriptionToggle", toggle);
            Set(row, "_descriptionToggleLabel", label);
            Set(row, "_progressTrack", root.transform.Find("ProgressTrack"));
            toggle.gameObject.SetActive(false);
            var accent = (RectTransform)root.transform.Find("Accent");
            accent.anchorMin = Vector2.zero;
            accent.anchorMax = new Vector2(0, 1);
            accent.pivot = new Vector2(0, 1);
            accent.anchoredPosition = Vector2.zero;
            accent.sizeDelta = new Vector2(4, 0);
        }

        private static void ConfigureBannerDescription(GameObject root)
        {
            var banner = (RectTransform)root.transform.Find("Banner");
            banner.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 156);
            var description = banner.Find("Description").GetComponent<TextMeshProUGUI>();
            description.rectTransform.anchoredPosition = new Vector2(140, -80);
            description.rectTransform.sizeDelta = new Vector2(710, 64);
            description.fontSize = 20;
            description.enableWordWrapping = true;
            description.overflowMode = TextOverflowModes.Ellipsis;
            description.alignment = TextAlignmentOptions.TopLeft;
            var icon = banner.Find("Icon").GetComponent<Image>();
            icon.rectTransform.anchoredPosition = new Vector2(22, -31);
            icon.preserveAspect = true;
        }

        [MenuItem("Poker/UI/Update Achievement Descriptions")]
        public static void UpdateDescriptions()
        {
            var rowPath = PanelDirectory + "AchievementPanel/AchievementRow.prefab";
            var root = PrefabUtility.LoadPrefabContents(rowPath);
            try
            {
                _font = root.transform.Find("Description").GetComponent<TextMeshProUGUI>().font;
                ConfigureRowDescription(root);
                ApplyRoundedStyle(root);
                PrefabUtility.SaveAsPrefabAsset(root, rowPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            var bannerPath = PanelDirectory + "AchievementPanel/AchievementBannerPanel.prefab";
            root = PrefabUtility.LoadPrefabContents(bannerPath);
            try
            {
                ConfigureBannerDescription(root);
                ApplyRoundedStyle(root);
                PrefabUtility.SaveAsPrefabAsset(root, bannerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RegisterPanels()
        {
            const string path = "Assets/Prefabs/Manager/UIManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<UIManager>());
                var panels = serialized.FindProperty("_panels");
                foreach (var name in new[] { "AchievementPanel", "AchievementBannerPanel" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelDirectory + "AchievementPanel/" + name + ".prefab");
                    if (Enumerable.Range(0, panels.arraySize).Any(i => panels.GetArrayElementAtIndex(i).objectReferenceValue == prefab)) continue;
                    panels.InsertArrayElementAtIndex(panels.arraySize);
                    panels.GetArrayElementAtIndex(panels.arraySize - 1).objectReferenceValue = prefab;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void AddTitleEntry()
        {
            var path = PanelDirectory + "TitlePanel/TitlePanel.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var title = root.GetComponent<TitlePanel>();
                var so = new SerializedObject(title);
                if (so.FindProperty("_achievements").objectReferenceValue != null) return;
                var tasks = (Button)so.FindProperty("_tasks").objectReferenceValue;
                var entry = UnityEngine.Object.Instantiate(tasks, tasks.transform.parent);
                entry.name = "AchievementsButton";
                entry.transform.SetSiblingIndex(tasks.transform.GetSiblingIndex() + 1);
                entry.GetComponentInChildren<TextMeshProUGUI>(true).text = "成就";
                entry.onClick.RemoveAllListeners();
                var features = new GameObject("ProgressFeatures", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                features.transform.SetParent(tasks.transform.parent, false);
                features.transform.SetSiblingIndex(tasks.transform.GetSiblingIndex());
                features.GetComponent<RectTransform>().sizeDelta = new Vector2(480, 76);
                var group = features.GetComponent<HorizontalLayoutGroup>();
                group.spacing = 12;
                group.childControlWidth = group.childControlHeight = true;
                group.childForceExpandWidth = group.childForceExpandHeight = true;
                tasks.transform.SetParent(features.transform, false);
                entry.transform.SetParent(features.transform, false);
                so.FindProperty("_achievements").objectReferenceValue = entry;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void RequireIcon(string name)
        {
            var path = "Assets/Resources/UI/Achievements/" + name + ".png";
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Restore the achievement icon or generate it following Assets/Resources/UI/Achievements/AGENTS.md.", path);
            }
        }
    }
}
