#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Framework.UI;
using Game.Input;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.UI.Editor
{
    public static class CoreUiPrefabMigration
    {
        private const string TitlePath = "Assets/Prefabs/UI/Panels/TitlePanel/TitlePanel.prefab";
        private const string LoginPath = "Assets/Prefabs/UI/Panels/LoginPanel/LoginPanel.prefab";
        private const string OptionPath = "Assets/Prefabs/UI/Panels/OptionPanel/OptionPanel.prefab";
        private const string InGameOptionPath = "Assets/Prefabs/UI/Panels/OptionPanel/InGameOptionPanel.prefab";
        private const string LobbyPath = "Assets/Prefabs/UI/Panels/LobbyPanel/LobbyPanel.prefab";
        private const string ExchangePath = "Assets/Prefabs/UI/Panels/ChipExchangePanel/ChipExchangePanel.prefab";
        private const string SwitchPath = "Assets/Prefabs/UI/Shared/SwitchControl.prefab";
        private const string CanvasPath = "Assets/Prefabs/UI/Canvas.prefab";
        private const string EventSystemPath = "Assets/Prefabs/UI/EventSystem.prefab";
        private const string MsgBoxPath = "Assets/Prefabs/UI/Panels/MsgBox/MsgBox.prefab";
        private const string UiManagerPath = "Assets/Prefabs/Manager/UIManager.prefab";

        private static readonly Color SurfaceColor = new(0.086f, 0.169f, 0.169f, 0.98f);
        private static readonly Color Gold = new(0.851f, 0.737f, 0.478f, 1f);

        [MenuItem("Tools/Poker/UI/Apply Core UI Update")]
        public static void ApplyAll()
        {
            ImportUiSprites();
            BuildLobbyPrefab();
            MigrateTitlePrefab();
            MigrateLoginPrefab();
            MigrateSettingsPrefab(OptionPath);
            MigrateSettingsPrefab(InGameOptionPath);
            BuildExchangePrefab();
            BuildSwitchPrefab();
            ConfigureMsgBoxShortcuts();
            ConfigureCanvas();
            ConfigureEventSystem();
            RegisterPanels();
            AssetDatabase.SaveAssets();
            Debug.Log("Core UI prefabs updated. Open each prefab for visual review before Play Mode.");
        }

        [MenuItem("Tools/Poker/UI/Apply Core Mobile Layout")]
        public static void ApplyMobileLayout()
        {
            ImportUiSprites();
            EditPrefab(TitlePath, RepairTitleLayout);
            EditPrefab(LobbyPath, RepairLobbyLayout);
            EditPrefab(LoginPath, RepairLoginLayout);
            EditPrefab(OptionPath, ConfigureSettingsMobileLayout);
            EditPrefab(InGameOptionPath, ConfigureSettingsMobileLayout);
            EditPrefab(ExchangePath, RebuildExchangeLayout);
            EditPrefab(SwitchPath, RepairSwitchPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log("Core mobile layouts updated.");
        }

        private static void ImportUiSprites()
        {
            string[] paths =
            {
                "Assets/Sprites/UITheme/Dollars-v1.png",
                "Assets/Sprites/UITheme/Chips-v1.png",
                "Assets/Sprites/UITheme/RoomsButton-v1.png",
                "Assets/Sprites/UITheme/LeaderboardButton-v1.png",
                "Assets/Sprites/UITheme/TasksButton-v1.png",
                "Assets/Sprites/UITheme/ShopButton-v1.png",
                "Assets/Sprites/UITheme/CollectionButton-v1.png",
                "Assets/Sprites/UITheme/SettingsButton-v1.png",
                "Assets/Sprites/UITheme/ExchangeButton-v1.png",
            };
            foreach (string path in paths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }
        }

        private static void BuildLobbyPrefab()
        {
            EnsureFolder("Assets/Prefabs/UI/Panels/LobbyPanel");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPath) == null)
                ClonePrefab(TitlePath, LobbyPath, "LobbyPanel");

            EditPrefab(LobbyPath, root =>
            {
                root.name = "LobbyPanel";
                RemoveComponent<TitlePanel>(root);
                var panel = GetOrAdd<LobbyPanel>(root);

                var rooms = Find(root, "RoomsButton") ?? Find(root, "PlayButton");
                var leaderboard = Find(root, "LeaderboardButton");
                var achievements = Find(root, "AchievementsButton");
                var tasks = Find(root, "TasksButton");
                var shop = Find(root, "ShopButton");
                var collection = Find(root, "CollectionButton");
                var settings = Find(root, "SettingsButton") ?? Find(root, "OptionButton");
                var exchange = Find(root, "ExchangeButton") ?? Find(root, "QuitButton");
                rooms.name = "RoomsButton";
                settings.name = "SettingsButton";
                exchange.name = "ExchangeButton";

                var grid = Find(root, "LobbyTiles") ?? NewUiObject("LobbyTiles", root.transform);
                var gridRect = grid.GetComponent<RectTransform>();
                SetAnchors(gridRect, new Vector2(0.11f, 0.12f), new Vector2(0.89f, 0.78f));
                var layout = GetOrAdd<GridLayoutGroup>(grid);
                layout.cellSize = new Vector2(320f, 255f);
                layout.spacing = new Vector2(34f, 28f);
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = 4;
                layout.childAlignment = TextAnchor.MiddleCenter;

                var tiles = new[] { rooms, leaderboard, achievements, tasks, shop, collection, settings, exchange };
                foreach (var tile in tiles)
                    tile.transform.SetParent(grid.transform, false);

                DecorateTile(rooms, "房间", "Assets/Sprites/UITheme/RoomsButton-v1.png");
                DecorateTile(leaderboard, "排行榜", "Assets/Sprites/UITheme/LeaderboardButton-v1.png");
                DecorateTile(achievements, "成就", "Assets/Resources/UI/Achievements/Default.png");
                DecorateTile(tasks, "每日任务", "Assets/Sprites/UITheme/TasksButton-v1.png");
                DecorateTile(shop, "商城", "Assets/Sprites/UITheme/ShopButton-v1.png");
                DecorateTile(collection, "收藏", "Assets/Sprites/UITheme/CollectionButton-v1.png");
                DecorateTile(settings, "设置", "Assets/Sprites/UITheme/SettingsButton-v1.png");
                DecorateTile(exchange, "筹码兑换", "Assets/Sprites/UITheme/ExchangeButton-v1.png");

                SetText(Find(root, "Title"), "大厅");
                var back = Find(root, "BackButton") ?? CloneNodeFromPrefab(LoginPath, "BackButton", root.transform);
                back.name = "BackButton";
                SetTopLeft(back.GetComponent<RectTransform>(), new Vector2(92f, -82f), new Vector2(72f, 72f));

                DestroyNamedExcept(root, new[] { grid }, "Buttons", "UtilityButtons", "AccountFeatures", "ProgressFeatures");
                SetReferences(panel, new Dictionary<string, Object>
                {
                    ["_back"] = back.GetComponent<Button>(),
                    ["_rooms"] = rooms.GetComponent<Button>(),
                    ["_leaderboard"] = leaderboard.GetComponent<Button>(),
                    ["_achievements"] = achievements.GetComponent<Button>(),
                    ["_tasks"] = tasks.GetComponent<Button>(),
                    ["_shop"] = shop.GetComponent<Button>(),
                    ["_collection"] = collection.GetComponent<Button>(),
                    ["_settings"] = settings.GetComponent<Button>(),
                    ["_exchange"] = exchange.GetComponent<Button>(),
                });
                ConfigureShortcutHandler(root, panel, null, back.GetComponent<Button>(), null,
                    tiles.Select(tile => (Selectable)tile.GetComponent<Button>()).Append(back.GetComponent<Button>()).ToArray());
                RepairLobbyLayout(root);
            });
        }

        private static void MigrateTitlePrefab()
        {
            EditPrefab(TitlePath, root =>
            {
                var play = Find(root, "PlayButton");
                var option = Find(root, "OptionButton");
                var buttons = Find(root, "Buttons") ?? NewUiObject("Buttons", root.transform);
                play.transform.SetParent(buttons.transform, false);
                option.transform.SetParent(buttons.transform, false);
                var rect = buttons.GetComponent<RectTransform>();
                SetAnchors(rect, new Vector2(0.36f, 0.18f), new Vector2(0.64f, 0.48f));
                var horizontalLayout = buttons.GetComponent<HorizontalLayoutGroup>();
                if (horizontalLayout != null)
                    Object.DestroyImmediate(horizontalLayout);
                var layout = GetOrAdd<VerticalLayoutGroup>(buttons);
                layout.spacing = 24f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlHeight = true;
                layout.childForceExpandHeight = true;
                SetText(play, "开始游戏");
                SetText(option, "设置");

                DestroyNamedExcept(root, new[] { buttons }, "QuitButton", "TasksButton", "ShopButton",
                    "CollectionButton", "LeaderboardButton", "AchievementsButton", "UtilityButtons",
                    "AccountFeatures", "ProgressFeatures");
                var panel = root.GetComponent<TitlePanel>();
                SetReferences(panel, new Dictionary<string, Object>
                {
                    ["_start"] = play.GetComponent<Button>(),
                    ["_option"] = option.GetComponent<Button>(),
                });
                ConfigureShortcutHandler(root, panel, play.GetComponent<Button>(), null, null,
                    new Selectable[] { play.GetComponent<Button>(), option.GetComponent<Button>() });
                RepairTitleLayout(root);
            });
        }

        private static void RepairLobbyLayout(GameObject root)
        {
            foreach (string childName in new[]
                     {
                         "ThemeTable", "ThemeBrand", "ThemeCaption", "ThemeAccent", "ThemeFooter",
                         "Decoration", "Decoration (1)", "Decoration (2)"
                     })
            {
                var child = FindPath(root, childName);
                if (child != null)
                    Object.DestroyImmediate(child);
            }

            var title = FindPath(root, "Title")?.GetComponent<TextMeshProUGUI>();
            if (title != null)
            {
                title.text = "大厅";
                title.enableAutoSizing = false;
                title.fontSize = 44f;
            }
            var back = FindPath(root, "BackButton")?.GetComponent<RectTransform>();
            var divider = FindPath(root, "LobbyHeaderDivider") ?? NewUiObject("LobbyHeaderDivider", root.transform);
            var dividerImage = GetOrAdd<Image>(divider);
            dividerImage.color = new Color(Gold.r, Gold.g, Gold.b, 0.62f);
            dividerImage.raycastTarget = false;

            var grid = FindPath(root, "LobbyTiles");
            if (grid == null)
                throw new InvalidOperationException("LobbyTiles is missing from LobbyPanel.");
            grid.transform.SetAsLastSibling();
            var gridRect = grid.GetComponent<RectTransform>();
            SetAnchors(gridRect, new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.78f));
            foreach (Transform tile in grid.transform)
            {
                var label = tile.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.enableAutoSizing = false;
                    label.fontSize = 28f;
                    label.color = new Color(0.94f, 0.92f, 0.84f, 1f);
                    label.alignment = TextAlignmentOptions.Center;
                    var labelRect = label.rectTransform;
                    labelRect.anchorMin = new Vector2(0.04f, 0.04f);
                    labelRect.anchorMax = new Vector2(0.96f, 0.29f);
                    labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                }
                var icon = tile.Find("Icon")?.GetComponent<RectTransform>();
                if (icon != null)
                {
                    icon.anchorMin = new Vector2(0.2f, 0.32f);
                    icon.anchorMax = new Vector2(0.8f, 0.92f);
                    icon.offsetMin = icon.offsetMax = Vector2.zero;
                }
            }

            var accountReferences = new Dictionary<string, Object>();
            var names = new[] { "UserName", "Dollars", "Chips" };
            var fields = new[] { "_userName", "_dollars", "_chips" };
            for (int i = 0; i < names.Length; i++)
            {
                var accountLabel = FindPath(root, names[i])?.GetComponent<TextMeshProUGUI>() ??
                    CloneTextFrom(title, root.transform, names[i], i == 0 ? string.Empty : "$0");
                accountLabel.fontSize = 28f;
                accountLabel.enableAutoSizing = false;
                accountLabel.enableWordWrapping = false;
                accountLabel.alignment = i == 0 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                accountLabel.overflowMode = i == 0 ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
                accountLabel.raycastTarget = false;
                var rect = accountLabel.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(i == 0 ? -840f : i == 1 ? -450f : -76f, -66f);
                rect.sizeDelta = new Vector2(i == 0 ? 550f : 280f, 64f);
                accountReferences[fields[i]] = accountLabel;
                if (i == 0) continue;
                var icon = FindPath(root, names[i] + "Icon") ?? NewUiObject(names[i] + "Icon", root.transform);
                var image = GetOrAdd<Image>(icon);
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UITheme/" + names[i] + "-v1.png");
                image.preserveAspect = true;
                image.raycastTarget = false;
                var iconRect = (RectTransform)icon.transform;
                iconRect.anchorMin = iconRect.anchorMax = Vector2.one;
                iconRect.anchoredPosition = new Vector2(i == 1 ? -775f : -400f, -66f);
                iconRect.sizeDelta = new Vector2(56f, 56f);
            }
            SetReferences(root.GetComponent<LobbyPanel>(), accountReferences);
        }

        private static void RepairTitleLayout(GameObject root)
        {
            var buttons = FindPath(root, "Buttons");
            var play = FindPath(root, "Buttons/PlayButton");
            var option = Find(root, "OptionButton");
            if (buttons == null || play == null || option == null)
                throw new InvalidOperationException("TitlePanel buttons are incomplete.");

            var layout = GetOrAdd<VerticalLayoutGroup>(buttons);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            AddLayout(play, 76f);
            var buttonsRect = buttons.GetComponent<RectTransform>();
            SetAnchors(buttonsRect, new Vector2(0.08f, 0.25f), new Vector2(0.36f, 0.40f));
            option.transform.SetParent(root.transform, false);
            SetTopLeft(option.GetComponent<RectTransform>(), new Vector2(-76f, -66f), new Vector2(64f, 64f));
            option.GetComponent<RectTransform>().anchorMin = option.GetComponent<RectTransform>().anchorMax = Vector2.one;
            var optionImage = option.GetComponent<Image>();
            optionImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UITheme/SettingsButton-v1.png");
            optionImage.type = Image.Type.Simple;
            optionImage.preserveAspect = true;
            StyleIconButton(option.GetComponent<Button>());
            foreach (var text in option.GetComponentsInChildren<TextMeshProUGUI>(true))
                text.gameObject.SetActive(false);
            var buttonLabels = new[]
            {
                play.GetComponentInChildren<TextMeshProUGUI>(true),
                option.GetComponentInChildren<TextMeshProUGUI>(true),
            };
            foreach (var label in buttonLabels)
            {
                label.enableAutoSizing = false;
                label.fontSize = 28f;
            }

        }

        private static void MigrateLoginPrefab()
        {
            EditPrefab(LoginPath, root =>
            {
                var loginTab = Find(root, "LoginTab") ?? Find(root, "LoginButton");
                var registerTab = Find(root, "RegisterTab") ?? Find(root, "RegisterButton");
                loginTab.name = "LoginTab";
                registerTab.name = "RegisterTab";
                SetText(loginTab, "登录");
                SetText(registerTab, "注册");

                var passwordRow = Find(root, "PasswordRow");
                var confirmationRow = Find(root, "PasswordConfirmationRow") ?? Object.Instantiate(passwordRow, passwordRow.transform.parent);
                confirmationRow.name = "PasswordConfirmationRow";
                confirmationRow.transform.SetSiblingIndex(passwordRow.transform.GetSiblingIndex() + 1);
                SetText(FindDirectOrDeep(confirmationRow, "Label"), "确认密码");
                var confirmation = confirmationRow.GetComponentInChildren<TMP_InputField>(true);
                confirmation.name = "PasswordConfirmationInput";
                confirmation.text = string.Empty;

                var submit = Find(root, "SubmitButton") ?? Object.Instantiate(loginTab, passwordRow.transform.parent);
                submit.name = "SubmitButton";
                submit.transform.SetSiblingIndex(confirmationRow.transform.GetSiblingIndex() + 1);
                SetText(submit, "登录");
                var submitLabel = submit.GetComponentInChildren<TextMeshProUGUI>(true);

                var loginSelected = EnsureSelectionMarker(loginTab);
                var registerSelected = EnsureSelectionMarker(registerTab);
                var back = Find(root, "BackButton");
                var userName = Find(root, "UserNameInput").GetComponent<TMP_InputField>();
                var password = Find(root, "PasswordInput").GetComponent<TMP_InputField>();
                var status = Find(root, "Status").GetComponent<TextMeshProUGUI>();
                var panel = root.GetComponent<LoginPanel>();
                SetReferences(panel, new Dictionary<string, Object>
                {
                    ["_userName"] = userName,
                    ["_password"] = password,
                    ["_passwordConfirmation"] = confirmation,
                    ["_passwordConfirmationGroup"] = confirmationRow,
                    ["_loginTab"] = loginTab.GetComponent<Button>(),
                    ["_registerTab"] = registerTab.GetComponent<Button>(),
                    ["_submit"] = submit.GetComponent<Button>(),
                    ["_back"] = back.GetComponent<Button>(),
                    ["_submitLabel"] = submitLabel,
                    ["_status"] = status,
                    ["_loginTabSelected"] = loginSelected,
                    ["_registerTabSelected"] = registerSelected,
                });
                ConfigureShortcutHandler(root, panel, submit.GetComponent<Button>(), back.GetComponent<Button>(), null,
                    new Selectable[] { loginTab.GetComponent<Button>(), registerTab.GetComponent<Button>(), userName,
                        password, confirmation, submit.GetComponent<Button>(), back.GetComponent<Button>() });
                RepairLoginLayout(root);
            });
        }

        private static void MigrateSettingsPrefab(string path)
        {
            EditPrefab(path, root =>
            {
                var viewObject = Find(root, "ShortcutSettings") ?? NewUiObject("ShortcutSettings", root.transform);
                var viewRect = viewObject.GetComponent<RectTransform>();
                SetAnchors(viewRect, new Vector2(0.54f, 0.16f), new Vector2(0.93f, 0.82f));
                var settingsObject = Find(root, "ThemeForm");
                if (settingsObject != null)
                {
                    SetAnchors(settingsObject.GetComponent<RectTransform>(),
                        new Vector2(0.07f, 0.16f), new Vector2(0.5f, 0.82f));
                }
                var background = GetOrAdd<Image>(viewObject);
                background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UITheme/Surface.png");
                background.type = Image.Type.Sliced;
                background.color = SurfaceColor;
                var vertical = GetOrAdd<VerticalLayoutGroup>(viewObject);
                vertical.padding = new RectOffset(26, 26, 22, 22);
                vertical.spacing = 10f;
                vertical.childControlHeight = true;
                vertical.childForceExpandHeight = false;

                var titleObject = Find(viewObject, "ShortcutTitle");
                var title = titleObject != null
                    ? titleObject.GetComponent<TextMeshProUGUI>()
                    : CloneText(root, viewObject.transform, "ShortcutTitle");
                title.text = "快捷键";
                title.fontSize = 30;
                AddLayout(title.gameObject, 46f);

                var sourceButton = Find(root, "AudioCreditsButton");
                var rows = new List<(PokerShortcutAction Shortcut, Button Button, TextMeshProUGUI Value)>();
                foreach (PokerShortcutAction shortcut in Enum.GetValues(typeof(PokerShortcutAction)))
                {
                    if (shortcut == PokerShortcutAction.Back) continue;
                    string rowName = shortcut + "Shortcut";
                    var row = Find(viewObject, rowName) ?? NewUiObject(rowName, viewObject.transform);
                    var horizontal = GetOrAdd<HorizontalLayoutGroup>(row);
                    horizontal.spacing = 12f;
                    horizontal.childAlignment = TextAnchor.MiddleCenter;
                    horizontal.childControlWidth = true;
                    horizontal.childForceExpandWidth = true;
                    AddLayout(row, 50f);

                    var label = Find(row, "Action")?.GetComponent<TextMeshProUGUI>() ?? CloneText(root, row.transform, "Action");
                    label.text = PokerShortcutBindings.GetActionLabel(shortcut);
                    label.fontSize = 23;
                    var buttonObject = Find(row, "Rebind") ?? Object.Instantiate(sourceButton, row.transform);
                    buttonObject.name = "Rebind";
                    SetText(buttonObject, PokerShortcutBindings.GetDisplayName(shortcut));
                    var value = buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
                    rows.Add((shortcut, buttonObject.GetComponent<Button>(), value));
                }

                var controls = Find(viewObject, "ShortcutControls") ?? NewUiObject("ShortcutControls", viewObject.transform);
                var controlsLayout = GetOrAdd<HorizontalLayoutGroup>(controls);
                controlsLayout.spacing = 12f;
                controlsLayout.childControlWidth = true;
                controlsLayout.childForceExpandWidth = true;
                AddLayout(controls, 52f);
                var reset = Find(controls, "ResetButton") ?? Object.Instantiate(sourceButton, controls.transform);
                reset.name = "ResetButton";
                SetText(reset, "恢复默认");
                var cancel = Find(controls, "CancelRebindButton") ?? Object.Instantiate(sourceButton, controls.transform);
                cancel.name = "CancelRebindButton";
                SetText(cancel, "取消改键");
                var status = Find(viewObject, "ShortcutStatus")?.GetComponent<TextMeshProUGUI>() ??
                    CloneText(root, viewObject.transform, "ShortcutStatus");
                status.text = string.Empty;
                status.fontSize = 20;
                AddLayout(status.gameObject, 34f);

                var view = GetOrAdd<ShortcutSettingsView>(viewObject);
                var serialized = new SerializedObject(view);
                var serializedRows = serialized.FindProperty("_rows");
                serializedRows.arraySize = rows.Count;
                for (int i = 0; i < rows.Count; i++)
                {
                    var rowProperty = serializedRows.GetArrayElementAtIndex(i);
                    rowProperty.FindPropertyRelative("_shortcut").enumValueIndex = (int)rows[i].Shortcut;
                    rowProperty.FindPropertyRelative("_rebindButton").objectReferenceValue = rows[i].Button;
                    rowProperty.FindPropertyRelative("_bindingLabel").objectReferenceValue = rows[i].Value;
                }
                serialized.FindProperty("_resetButton").objectReferenceValue = reset.GetComponent<Button>();
                serialized.FindProperty("_cancelRebindButton").objectReferenceValue = cancel.GetComponent<Button>();
                serialized.FindProperty("_rebindPrompt").objectReferenceValue = null;
                serialized.FindProperty("_status").objectReferenceValue = status;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var panel = root.GetComponent<BasePanel>();
                var back = Find(root, "BackButton")?.GetComponent<Button>();
                var focus = root.GetComponentsInChildren<Selectable>(true)
                    .Where(item => item != null && item.gameObject.activeSelf).ToArray();
                ConfigureShortcutHandler(root, panel, null, back, null, focus);
                ConfigureSettingsMobileLayout(root);
            });
        }

        private static void BuildExchangePrefab()
        {
            EnsureFolder("Assets/Prefabs/UI/Panels/ChipExchangePanel");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ExchangePath) == null)
                ClonePrefab(LoginPath, ExchangePath, "ChipExchangePanel");

            EditPrefab(ExchangePath, root =>
            {
                root.name = "ChipExchangePanel";
                RemoveComponent<LoginPanel>(root);
                RemoveComponent<PanelShortcutHandler>(root);
                var panel = GetOrAdd<ChipExchangePanel>(root);
                SetText(Find(root, "Title"), "筹码兑换");

                var back = Find(root, "BackButton");
                var dollarsRow = Find(root, "DollarsBalance") ?? Find(root, "UserNameRow");
                var chipsRow = Find(root, "ChipsBalance") ?? Find(root, "PasswordRow");
                dollarsRow.name = "DollarsBalance";
                chipsRow.name = "ChipsBalance";
                SetText(FindDirectOrDeep(dollarsRow, "Label"), "美金");
                SetText(FindDirectOrDeep(chipsRow, "Label"), "筹码");
                var dollarsInput = dollarsRow.GetComponentInChildren<TMP_InputField>(true);
                var chipsInput = chipsRow.GetComponentInChildren<TMP_InputField>(true);
                var dollars = dollarsInput.textComponent;
                var chips = chipsInput.textComponent;
                dollarsInput.contentType = TMP_InputField.ContentType.Standard;
                chipsInput.contentType = TMP_InputField.ContentType.Standard;
                if (dollarsInput.placeholder != null)
                    dollarsInput.placeholder.gameObject.SetActive(false);
                if (chipsInput.placeholder != null)
                    chipsInput.placeholder.gameObject.SetActive(false);
                dollarsInput.enabled = false;
                chipsInput.enabled = false;
                dollars.text = "$0";
                chips.text = "$0";
                AddCurrencyIcon(dollarsRow, "Assets/Sprites/UITheme/Dollars-v1.png");
                AddCurrencyIcon(chipsRow, "Assets/Sprites/UITheme/Chips-v1.png");

                var available = Find(chipsRow, "AvailableChips")?.GetComponent<TextMeshProUGUI>() ??
                    CloneText(root, chipsRow.transform, "AvailableChips");
                available.text = string.Empty;
                available.fontSize = 20;

                var toChips = Find(root, "LoginTab") ?? Find(root, "LoginButton");
                var toDollars = Find(root, "RegisterTab") ?? Find(root, "RegisterButton");
                toChips.name = "DollarsToChips";
                toDollars.name = "ChipsToDollars";
                SetText(toChips, "美金 → 筹码");
                SetText(toDollars, "筹码 → 美金");
                var toChipsSelected = EnsureSelectionMarker(toChips);
                var toDollarsSelected = EnsureSelectionMarker(toDollars);

                var amountGroup = Find(root, "ExchangeAmount") ?? Find(root, "PasswordConfirmationRow");
                amountGroup.name = "ExchangeAmount";
                amountGroup.SetActive(true);
                SetText(FindDirectOrDeep(amountGroup, "Label"), "金额");
                var amount = amountGroup.GetComponentInChildren<TMP_InputField>(true);
                amount.name = "AmountInput";
                amount.text = string.Empty;
                amount.contentType = TMP_InputField.ContentType.IntegerNumber;

                var exchange = Find(root, "ExchangeButton") ?? Find(root, "SubmitButton");
                exchange.name = "ExchangeButton";
                SetText(exchange, "兑换筹码");
                var exchangeLabel = exchange.GetComponentInChildren<TextMeshProUGUI>(true);
                var all = Find(root, "AllButton") ?? Object.Instantiate(exchange, exchange.transform.parent);
                all.name = "AllButton";
                SetText(all, "全部");
                var refresh = Find(root, "RefreshButton") ?? Object.Instantiate(exchange, exchange.transform.parent);
                refresh.name = "RefreshButton";
                SetText(refresh, "刷新");
                var status = Find(root, "Status").GetComponent<TextMeshProUGUI>();
                var busy = Find(root, "BusyIndicator") ?? NewUiObject("BusyIndicator", root.transform);
                busy.SetActive(false);

                SetReferences(panel, new Dictionary<string, Object>
                {
                    ["_back"] = back.GetComponent<Button>(),
                    ["_refresh"] = refresh.GetComponent<Button>(),
                    ["_dollars"] = dollars,
                    ["_chips"] = chips,
                    ["_availableChips"] = available,
                    ["_dollarsToChips"] = toChips.GetComponent<Button>(),
                    ["_chipsToDollars"] = toDollars.GetComponent<Button>(),
                    ["_dollarsToChipsSelected"] = toChipsSelected,
                    ["_chipsToDollarsSelected"] = toDollarsSelected,
                    ["_amount"] = amount,
                    ["_all"] = all.GetComponent<Button>(),
                    ["_exchange"] = exchange.GetComponent<Button>(),
                    ["_exchangeLabel"] = exchangeLabel,
                    ["_status"] = status,
                    ["_busyIndicator"] = busy,
                });
                ConfigureShortcutHandler(root, panel, exchange.GetComponent<Button>(), back.GetComponent<Button>(), null,
                    new Selectable[] { toChips.GetComponent<Button>(), toDollars.GetComponent<Button>(), amount,
                        all.GetComponent<Button>(), exchange.GetComponent<Button>(), refresh.GetComponent<Button>(),
                        back.GetComponent<Button>() });
                RebuildExchangeLayout(root);
            });
        }

        private static void ConfigureTabbedRow(GameObject root, string firstName, string secondName, string rowName)
        {
            var content = Find(root, "Buttons");
            var first = Find(root, firstName);
            var second = Find(root, secondName);
            if (content == null || first == null || second == null)
                return;

            var firstField = content.transform.Find("UserNameRow") ?? content.transform.Find("DollarsBalance");
            int siblingIndex = firstField == null
                ? Mathf.Min(first.transform.GetSiblingIndex(), second.transform.GetSiblingIndex())
                : firstField.GetSiblingIndex();
            var row = Find(root, rowName) ?? NewUiObject(rowName, content.transform);
            row.transform.SetParent(content.transform, false);
            row.transform.SetSiblingIndex(siblingIndex);
            var layout = GetOrAdd<HorizontalLayoutGroup>(row);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            AddLayout(row, 74f);

            first.transform.SetParent(row.transform, false);
            second.transform.SetParent(row.transform, false);
            foreach (var tab in new[] { first, second })
            {
                var element = GetOrAdd<LayoutElement>(tab);
                element.minWidth = 0f;
                element.flexibleWidth = 1f;
                element.minHeight = 0f;
                element.preferredHeight = -1f;
            }
        }

        private static void ConfigureFormTouchLayout(GameObject root)
        {
            var surface = FindPath(root, "ThemeForm")?.GetComponent<RectTransform>();
            var content = FindPath(root, "Buttons")?.GetComponent<RectTransform>();
            if (surface == null || content == null)
                return;

            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
            }
            var touchRows = new List<LayoutElement>();
            var displayRows = new List<LayoutElement>();
            var statusRows = new List<LayoutElement>();
            var fieldRows = new List<HorizontalLayoutGroup>();
            var fieldLabels = new List<LayoutElement>();
            var fieldValues = new List<LayoutElement>();
            foreach (Transform child in content)
            {
                if (!child.gameObject.activeSelf && child.name != "PasswordConfirmationRow")
                    continue;
                if (child.name == "Status")
                {
                    statusRows.Add(AddLayout(child.gameObject, 54f));
                    continue;
                }
                if (child.GetComponentInChildren<Selectable>(true) != null)
                    touchRows.Add(AddLayout(child.gameObject, 74f));
                else
                    displayRows.Add(AddLayout(child.gameObject, 96f));

                var input = child.GetComponentInChildren<TMP_InputField>(true);
                var horizontal = child.GetComponent<HorizontalLayoutGroup>();
                if (input == null || horizontal == null)
                    continue;
                horizontal.childControlWidth = true;
                horizontal.childControlHeight = true;
                horizontal.childForceExpandWidth = false;
                horizontal.childForceExpandHeight = true;
                fieldRows.Add(horizontal);
                var label = (child.Find("TabName") ?? child.Find("AmountLabel"))?.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    var labelLayout = GetOrAdd<LayoutElement>(label.gameObject);
                    labelLayout.minWidth = 250f;
                    labelLayout.preferredWidth = 250f;
                    labelLayout.flexibleWidth = 0f;
                    fieldLabels.Add(labelLayout);
                }
                var inputLayout = GetOrAdd<LayoutElement>(input.gameObject);
                inputLayout.minWidth = 0f;
                inputLayout.preferredWidth = -1f;
                inputLayout.flexibleWidth = 1f;
                fieldValues.Add(inputLayout);
            }


            foreach (var text in content.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.enableAutoSizing = false;
                text.fontSize = 26f;
            }
        }

        private static void RepairLoginLayout(GameObject root)
        {
            // Keep the migration compatible with both the original and compact hierarchy.
            Find(root, "Buttons").transform.SetParent(root.transform, false);
            ConfigureTabbedRow(root, "LoginTab", "RegisterTab", "AuthTabs");
            var content = FindPath(root, "Buttons");
            var tabs = content?.transform.Find("AuthTabs");
            var userRow = content?.transform.Find("UserNameRow");
            if (tabs != null && userRow != null)
            {
                tabs.SetSiblingIndex(0);
                userRow.SetSiblingIndex(1);
            }

            var confirmationRow = content?.transform.Find("PasswordConfirmationRow");
            var confirmationLabel = confirmationRow?.Find("TabName")?.GetComponent<TextMeshProUGUI>();
            if (confirmationLabel != null)
                confirmationLabel.text = "确认密码";
            var confirmationInput = confirmationRow?.GetComponentInChildren<TMP_InputField>(true);
            if (confirmationInput?.placeholder is TextMeshProUGUI placeholder)
                placeholder.text = "请再次输入密码";

            var subtitle = FindPath(root, "ThemeSubtitle");
            if (subtitle != null)
                subtitle.SetActive(false);
            ConfigureFormTouchLayout(root);
            ConfigureCompactLoginForm(root);

            var panel = root.GetComponent<LoginPanel>();
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("_title").objectReferenceValue =
                FindPath(root, "Title")?.GetComponent<TextMeshProUGUI>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCompactLoginForm(GameObject root)
        {
            var surface = FindPath(root, "ThemeForm").GetComponent<RectTransform>();
            var content = FindPath(root, "Buttons").GetComponent<RectTransform>();
            var tabs = Find(root, "AuthTabs").GetComponent<RectTransform>();
            tabs.SetParent(root.transform, false);
            foreach (var rect in new[] { surface, content, tabs })
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.48f);
                rect.pivot = new Vector2(0.5f, 0.5f);
            }
            surface.anchoredPosition = Vector2.zero;
            surface.sizeDelta = new Vector2(880f, 540f);
            tabs.anchoredPosition = new Vector2(0f, 206f);
            tabs.sizeDelta = new Vector2(784f, 64f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(0f, 150f);
            content.sizeDelta = new Vector2(784f, 388f);
            tabs.SetParent(surface, false);
            content.SetParent(surface, false);
            tabs.SetSiblingIndex(0);
            content.SetSiblingIndex(1);
            var surfaceLayout = GetOrAdd<VerticalLayoutGroup>(surface.gameObject);
            surfaceLayout.padding = new RectOffset(48, 48, 32, 24);
            surfaceLayout.spacing = 24f;
            surfaceLayout.childControlWidth = surfaceLayout.childControlHeight = true;
            surfaceLayout.childForceExpandWidth = true;
            surfaceLayout.childForceExpandHeight = false;
            var surfaceFit = GetOrAdd<ContentSizeFitter>(surface.gameObject);
            surfaceFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            surfaceFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            AddLayout(tabs.gameObject, 64f).flexibleHeight = 0f;
            var vertical = content.GetComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset();
            vertical.spacing = 16f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childForceExpandHeight = false;
            foreach (Transform child in content)
            {
                var row = AddLayout(child.gameObject, child.name == "Status" ? 48f : 64f);
                row.flexibleHeight = 0f;
                var label = child.Find("TabName");
                if (label != null)
                {
                    var labelLayout = GetOrAdd<LayoutElement>(label.gameObject);
                    labelLayout.minWidth = labelLayout.preferredWidth = 160f;
                    labelLayout.flexibleWidth = 0f;
                }
                foreach (var input in child.GetComponentsInChildren<TMP_InputField>(true))
                {
                    var field = GetOrAdd<LayoutElement>(input.gameObject);
                    field.minHeight = field.preferredHeight = 64f;
                    field.flexibleHeight = 0f;
                }
            }
        }

        private static void RebuildExchangeLayout(GameObject root)
        {
            var oldContent = FindPath(root, "Buttons");
            if (oldContent == null)
                throw new InvalidOperationException("ChipExchangePanel/Buttons is missing.");
            var buttonTemplate = oldContent.transform.Find("ExchangeButton")?.gameObject;
            var amountInputTemplate = oldContent.transform.Find("ExchangeAmount/AmountInput")?.gameObject;
            var textTemplate = oldContent.transform.Find("ExchangeAmount/TabName")?.GetComponent<TextMeshProUGUI>() ??
                oldContent.transform.Find("ExchangeAmount/AmountLabel")?.GetComponent<TextMeshProUGUI>();
            if (buttonTemplate == null || amountInputTemplate == null || textTemplate == null)
                throw new InvalidOperationException("ChipExchangePanel templates are incomplete.");

            var content = NewUiObject("ExchangeContent", root.transform);
            content.transform.SetSiblingIndex(oldContent.transform.GetSiblingIndex());
            var vertical = GetOrAdd<VerticalLayoutGroup>(content);
            vertical.padding = new RectOffset(26, 26, 20, 20);
            vertical.spacing = 12f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childForceExpandWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandHeight = false;

            TextMeshProUGUI dollars;
            TextMeshProUGUI unusedAvailable;
            CreateBalanceRow(content.transform, "DollarsBalance", "美金",
                "Assets/Sprites/UITheme/Dollars-v1.png", textTemplate, false, out dollars, out unusedAvailable);
            TextMeshProUGUI chips;
            TextMeshProUGUI availableChips;
            CreateBalanceRow(content.transform, "ChipsBalance", "筹码",
                "Assets/Sprites/UITheme/Chips-v1.png", textTemplate, true, out chips, out availableChips);

            var directionTabs = CreateHorizontalRow("DirectionTabs", content.transform, 74f);
            var toChips = CloneButton(buttonTemplate, directionTabs.transform, "DollarsToChips", "美金 → 筹码");
            var toDollars = CloneButton(buttonTemplate, directionTabs.transform, "ChipsToDollars", "筹码 → 美金");
            ConfigureEqualWidthChildren(directionTabs);
            var toChipsSelected = EnsureSelectionMarker(toChips);
            var toDollarsSelected = EnsureSelectionMarker(toDollars);

            var amountRow = CreateHorizontalRow("ExchangeAmount", content.transform, 74f);
            var amountLabel = CloneTextFrom(textTemplate, amountRow.transform, "AmountLabel", "金额");
            var amountLabelLayout = GetOrAdd<LayoutElement>(amountLabel.gameObject);
            amountLabelLayout.minWidth = 250f;
            amountLabelLayout.preferredWidth = 250f;
            var amountObject = Object.Instantiate(amountInputTemplate, amountRow.transform);
            amountObject.name = "AmountInput";
            amountObject.SetActive(true);
            var amount = amountObject.GetComponent<TMP_InputField>();
            amount.enabled = true;
            amount.interactable = true;
            amount.text = string.Empty;
            amount.contentType = TMP_InputField.ContentType.IntegerNumber;
            if (amount.placeholder is TextMeshProUGUI amountPlaceholder)
                amountPlaceholder.text = "输入兑换金额";
            var amountLayout = GetOrAdd<LayoutElement>(amountObject);
            amountLayout.minWidth = 0f;
            amountLayout.preferredWidth = -1f;
            amountLayout.flexibleWidth = 1f;

            var actions = CreateHorizontalRow("ExchangeActions", content.transform, 74f);
            var all = CloneButton(buttonTemplate, actions.transform, "AllButton", "全部");
            var refresh = CloneButton(buttonTemplate, actions.transform, "RefreshButton", "刷新");
            ConfigureEqualWidthChildren(actions);
            var exchange = CloneButton(buttonTemplate, content.transform, "ExchangeButton", "兑换筹码");
            AddLayout(exchange, 74f);
            var exchangeLabel = exchange.GetComponentInChildren<TextMeshProUGUI>(true);

            var status = CloneTextFrom(textTemplate, content.transform, "Status", string.Empty);
            status.alignment = TextAlignmentOptions.Center;
            AddLayout(status.gameObject, 54f);

            Object.DestroyImmediate(oldContent);
            content.name = "Buttons";
            var subtitle = FindPath(root, "ThemeSubtitle");
            if (subtitle != null)
                Object.DestroyImmediate(subtitle);

            var busy = FindPath(root, "BusyIndicator") ?? NewUiObject("BusyIndicator", root.transform);
            busy.SetActive(false);
            var panel = root.GetComponent<ChipExchangePanel>();
            var back = FindPath(root, "BackButton")?.GetComponent<Button>();
            SetReferences(panel, new Dictionary<string, Object>
            {
                ["_back"] = back,
                ["_refresh"] = refresh.GetComponent<Button>(),
                ["_dollars"] = dollars,
                ["_chips"] = chips,
                ["_availableChips"] = availableChips,
                ["_dollarsToChips"] = toChips.GetComponent<Button>(),
                ["_chipsToDollars"] = toDollars.GetComponent<Button>(),
                ["_dollarsToChipsSelected"] = toChipsSelected,
                ["_chipsToDollarsSelected"] = toDollarsSelected,
                ["_amount"] = amount,
                ["_all"] = all.GetComponent<Button>(),
                ["_exchange"] = exchange.GetComponent<Button>(),
                ["_exchangeLabel"] = exchangeLabel,
                ["_status"] = status,
                ["_busyIndicator"] = busy,
            });
            ConfigureShortcutHandler(root, panel, exchange.GetComponent<Button>(), back, amount,
                new Selectable[]
                {
                    toChips.GetComponent<Button>(), toDollars.GetComponent<Button>(), amount,
                    all.GetComponent<Button>(), refresh.GetComponent<Button>(), exchange.GetComponent<Button>(), back
                });
            ConfigureFormTouchLayout(root);
        }

        private static void CreateBalanceRow(
            Transform parent,
            string rowName,
            string labelText,
            string iconPath,
            TextMeshProUGUI textTemplate,
            bool includeAvailable,
            out TextMeshProUGUI amount,
            out TextMeshProUGUI available)
        {
            var row = CreateHorizontalRow(rowName, parent, 96f);
            var background = GetOrAdd<Image>(row);
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UITheme/Surface.png");
            background.type = Image.Type.Sliced;
            background.color = new Color(0.035f, 0.11f, 0.105f, 0.9f);

            var iconObject = NewUiObject("CurrencyIcon", row.transform);
            var icon = GetOrAdd<Image>(iconObject);
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconLayout = GetOrAdd<LayoutElement>(iconObject);
            iconLayout.minWidth = 64f;
            iconLayout.preferredWidth = 64f;

            var label = CloneTextFrom(textTemplate, row.transform, "CurrencyLabel", labelText);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var labelLayout = GetOrAdd<LayoutElement>(label.gameObject);
            labelLayout.minWidth = 160f;
            labelLayout.preferredWidth = 160f;

            var values = NewUiObject("BalanceValues", row.transform);
            var valuesLayout = GetOrAdd<VerticalLayoutGroup>(values);
            valuesLayout.childAlignment = TextAnchor.MiddleRight;
            valuesLayout.childControlWidth = true;
            valuesLayout.childForceExpandWidth = true;
            valuesLayout.childControlHeight = true;
            valuesLayout.childForceExpandHeight = true;
            var valuesElement = GetOrAdd<LayoutElement>(values);
            valuesElement.minWidth = 0f;
            valuesElement.preferredWidth = -1f;
            valuesElement.flexibleWidth = 1f;

            amount = CloneTextFrom(textTemplate, values.transform, "Balance", "$0");
            amount.alignment = TextAlignmentOptions.MidlineRight;
            if (includeAvailable)
            {
                available = CloneTextFrom(textTemplate, values.transform, "AvailableChips", string.Empty);
                available.alignment = TextAlignmentOptions.MidlineRight;
                available.fontSize = 20f;
            }
            else
            {
                available = null;
            }
        }

        private static GameObject CreateHorizontalRow(string name, Transform parent, float height)
        {
            var row = NewUiObject(name, parent);
            AddLayout(row, height);
            var layout = GetOrAdd<HorizontalLayoutGroup>(row);
            layout.padding = new RectOffset(22, 22, 10, 10);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            return row;
        }

        private static GameObject CloneButton(GameObject template, Transform parent, string name, string text)
        {
            var button = Object.Instantiate(template, parent);
            button.name = name;
            button.SetActive(true);
            SetText(button, text);
            return button;
        }

        private static TextMeshProUGUI CloneTextFrom(
            TextMeshProUGUI template,
            Transform parent,
            string name,
            string text)
        {
            var clone = Object.Instantiate(template.gameObject, parent);
            clone.name = name;
            var label = clone.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.enableAutoSizing = false;
            label.fontSize = 26f;
            return label;
        }

        private static void ConfigureEqualWidthChildren(GameObject row)
        {
            foreach (Transform child in row.transform)
            {
                var layout = GetOrAdd<LayoutElement>(child.gameObject);
                layout.minWidth = 0f;
                layout.preferredWidth = -1f;
                layout.flexibleWidth = 1f;
                layout.minHeight = 0f;
                layout.preferredHeight = -1f;
            }
        }


        private static void ConfigureSettingsMobileLayout(GameObject root)
        {
            if (root.GetComponent<InGameOptionPanel>() != null)
            {
                var obsoleteTasksButton = FindPath(root, "DailyTasksButton");
                if (obsoleteTasksButton != null)
                    Object.DestroyImmediate(obsoleteTasksButton);
            }

            var surface = FindPath(root, "ThemeForm")?.GetComponent<RectTransform>();
            var settingsContentObject = FindPath(root, "SettingsViewport/SettingsMask/Buttons") ??
                FindPath(root, "Buttons");
            var settingsContent = settingsContentObject?.GetComponent<RectTransform>();
            var shortcuts = FindPath(root, "ShortcutSettings");
            if (surface == null || settingsContent == null || shortcuts == null)
                return;

            var settingsHost = FindPath(root, "SettingsViewport") ?? NewUiObject("SettingsViewport", root.transform);
            var settingsMask = FindPath(settingsHost, "SettingsMask") ?? NewUiObject("SettingsMask", settingsHost.transform);

            var subtitle = FindPath(root, "ThemeSubtitle") ??
                FindPath(root, "SettingsViewport/SettingsMask/Buttons/ThemeSubtitle");
            if (subtitle != null)
            {
                subtitle.transform.SetParent(settingsContent, false);
                subtitle.transform.SetSiblingIndex(0);
            }
            var audioCredits = FindPath(root, "AudioCreditsButton") ??
                FindPath(root, "SettingsViewport/SettingsMask/Buttons/AudioCreditsButton");
            if (audioCredits != null)
            {
                audioCredits.transform.SetParent(settingsContent, false);
                audioCredits.transform.SetAsLastSibling();
            }

            var settingsLayout = GetOrAdd<VerticalLayoutGroup>(settingsContent.gameObject);
            settingsLayout.padding = new RectOffset(30, 30, 20, 20);
            settingsLayout.spacing = 12f;
            settingsLayout.childControlWidth = true;
            settingsLayout.childForceExpandWidth = true;
            settingsLayout.childControlHeight = true;
            settingsLayout.childForceExpandHeight = false;
            ConfigureScrollView(settingsHost, settingsMask, settingsContent.gameObject, 20f);
            if (settingsHost.transform.GetSiblingIndex() <= surface.transform.GetSiblingIndex())
                settingsHost.transform.SetSiblingIndex(surface.transform.GetSiblingIndex() + 1);

            var fullScreenToggle = FindPath(settingsContentObject, "FullScreen/Toggle")?.GetComponent<Toggle>() ??
                                   FindPath(settingsContentObject, "FullScreen")?.GetComponentInChildren<Toggle>(true);
            if (fullScreenToggle != null)
                RepairSwitchControl(fullScreenToggle.gameObject);

            var shortcutViewport = FindPath(shortcuts, "ShortcutViewport") ??
                NewUiObject("ShortcutViewport", shortcuts.transform);
            var shortcutContent = FindPath(shortcuts, "ShortcutViewport/ShortcutContent") ??
                NewUiObject("ShortcutContent", shortcutViewport.transform);
            var shortcutItems = new List<GameObject>();
            var title = FindPath(shortcuts, "ShortcutViewport/ShortcutContent/ShortcutTitle") ??
                FindPath(shortcuts, "ShortcutTitle");
            if (title != null)
                shortcutItems.Add(title);
            foreach (PokerShortcutAction shortcut in Enum.GetValues(typeof(PokerShortcutAction)))
            {
                if (shortcut == PokerShortcutAction.Back) continue;
                var row = FindPath(shortcuts, "ShortcutViewport/ShortcutContent/" + shortcut + "Shortcut") ??
                    FindPath(shortcuts, shortcut + "Shortcut");
                if (row != null)
                {
                    shortcutItems.Add(row);
                    var actionLabel = FindPath(row, "Action")?.GetComponent<TextMeshProUGUI>();
                    if (actionLabel != null)
                        actionLabel.text = PokerShortcutBindings.GetActionLabel(shortcut);
                }
            }
            var controls = FindPath(shortcuts, "ShortcutViewport/ShortcutContent/ShortcutControls") ??
                FindPath(shortcuts, "ShortcutControls");
            var status = FindPath(shortcuts, "ShortcutViewport/ShortcutContent/ShortcutStatus") ??
                FindPath(shortcuts, "ShortcutStatus");
            if (controls != null)
                shortcutItems.Add(controls);
            if (status != null)
                shortcutItems.Add(status);
            foreach (var item in shortcutItems)
                item.transform.SetParent(shortcutContent.transform, false);

            var outerLayout = shortcuts.GetComponent<VerticalLayoutGroup>();
            if (outerLayout != null)
                Object.DestroyImmediate(outerLayout);
            var shortcutLayout = GetOrAdd<VerticalLayoutGroup>(shortcutContent);
            shortcutLayout.padding = new RectOffset(26, 26, 22, 22);
            shortcutLayout.spacing = 10f;
            shortcutLayout.childControlWidth = true;
            shortcutLayout.childForceExpandWidth = true;
            shortcutLayout.childControlHeight = true;
            shortcutLayout.childForceExpandHeight = false;
            ConfigureScrollView(shortcuts, shortcutViewport, shortcutContent, 0f);

            var settingsRows = new List<LayoutElement>();
            var settingsLayouts = new List<HorizontalLayoutGroup>();
            var settingsLabelColumns = new List<LayoutElement>();
            var settingsValueColumns = new List<LayoutElement>();
            LayoutElement settingsSubtitle = null;
            foreach (Transform child in settingsContent)
            {
                if (!child.gameObject.activeSelf)
                    continue;
                if (child.gameObject == subtitle)
                {
                    settingsSubtitle = AddLayout(child.gameObject, 56f);
                    continue;
                }
                settingsRows.Add(AddLayout(child.gameObject, 74f));
                var rowLayout = child.GetComponent<HorizontalLayoutGroup>();
                if (rowLayout == null)
                    continue;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childForceExpandHeight = true;
                settingsLayouts.Add(rowLayout);
                bool first = true;
                foreach (Transform column in child)
                {
                    if (!column.gameObject.activeSelf)
                        continue;
                    var columnLayout = GetOrAdd<LayoutElement>(column.gameObject);
                    if (first)
                    {
                        columnLayout.minWidth = 220f;
                        columnLayout.preferredWidth = 220f;
                        columnLayout.flexibleWidth = 0f;
                        settingsLabelColumns.Add(columnLayout);
                        first = false;
                    }
                    else
                    {
                        columnLayout.minWidth = 0f;
                        columnLayout.preferredWidth = -1f;
                        columnLayout.flexibleWidth = 1f;
                        settingsValueColumns.Add(columnLayout);
                    }
                }
            }
            var shortcutRows = shortcutItems
                .Where(item => item.name.EndsWith("Shortcut", StringComparison.Ordinal))
                .Select(item => AddLayout(item, 50f)).ToArray();
            var shortcutLayouts = new List<HorizontalLayoutGroup>();
            var shortcutActionColumns = new List<LayoutElement>();
            var shortcutValueColumns = new List<LayoutElement>();
            foreach (var item in shortcutItems.Where(item => item.name.EndsWith("Shortcut", StringComparison.Ordinal)))
            {
                var rowLayout = item.GetComponent<HorizontalLayoutGroup>();
                if (rowLayout != null)
                {
                    rowLayout.childControlWidth = true;
                    rowLayout.childControlHeight = true;
                    rowLayout.childForceExpandWidth = false;
                    rowLayout.childForceExpandHeight = true;
                    shortcutLayouts.Add(rowLayout);
                }
                var action = item.transform.Find("Action");
                var value = item.transform.Find("Rebind");
                if (action != null)
                {
                    var actionLayout = GetOrAdd<LayoutElement>(action.gameObject);
                    actionLayout.minWidth = 240f;
                    actionLayout.preferredWidth = 240f;
                    actionLayout.flexibleWidth = 1f;
                    shortcutActionColumns.Add(actionLayout);
                }
                if (value != null)
                {
                    var valueLayout = GetOrAdd<LayoutElement>(value.gameObject);
                    valueLayout.minWidth = 300f;
                    valueLayout.preferredWidth = 300f;
                    valueLayout.flexibleWidth = 0f;
                    shortcutValueColumns.Add(valueLayout);
                }
            }

            if (controls != null)
            {
                var controlsLayout = controls.GetComponent<HorizontalLayoutGroup>();
                if (controlsLayout != null)
                {
                    controlsLayout.childControlWidth = true;
                    controlsLayout.childControlHeight = true;
                    controlsLayout.childForceExpandWidth = true;
                    controlsLayout.childForceExpandHeight = true;
                }
            }


            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.enableAutoSizing = false;
                if (text.transform.IsChildOf(settingsContent) || text.transform.IsChildOf(shortcutContent.transform))
                    text.fontSize = 24f;
            }
        }

        private static void ConfigureScrollView(
            GameObject host,
            GameObject viewport,
            GameObject content,
            float inset)
        {
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(inset, inset);
            viewportRect.offsetMax = new Vector2(-inset, -inset);
            var viewportImage = GetOrAdd<Image>(viewport);
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
            GetOrAdd<RectMask2D>(viewport);

            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            var fitter = GetOrAdd<ContentSizeFitter>(content);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = GetOrAdd<ScrollRect>(host);
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 90f;
        }

        private static void BuildSwitchPrefab()
        {
            EnsureFolder("Assets/Prefabs/UI/Shared");
            var root = NewUiObject("SwitchControl", null);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(112f, 52f);
            RepairSwitchControl(root);
            PrefabUtility.SaveAsPrefabAsset(root, SwitchPath);
            Object.DestroyImmediate(root);
        }

        private static void RepairSwitchPrefab(GameObject root)
        {
            RepairSwitchControl(root);
        }

        private static void RepairSwitchControl(GameObject root)
        {
            var obsoleteBackground = FindPath(root, "Background");
            if (obsoleteBackground != null)
                Object.DestroyImmediate(obsoleteBackground);

            var hitArea = GetOrAdd<Image>(root);
            hitArea.color = new Color(1f, 1f, 1f, 0f);
            hitArea.raycastTarget = true;

            var toggle = GetOrAdd<Toggle>(root);
            toggle.targetGraphic = hitArea;
            toggle.transition = Selectable.Transition.None;
            toggle.graphic = null;

            var trackObject = FindPath(root, "Track") ?? NewUiObject("Track", root.transform);
            trackObject.transform.SetAsFirstSibling();
            var track = GetOrAdd<Image>(trackObject);
            track.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UITheme/Surface.png");
            track.type = Image.Type.Sliced;
            track.raycastTarget = false;

            var handleObject = FindPath(root, "Track/Handle") ?? FindPath(root, "Handle") ??
                NewUiObject("Handle", trackObject.transform);
            handleObject.transform.SetParent(trackObject.transform, false);
            handleObject.transform.SetAsLastSibling();
            foreach (Transform sibling in trackObject.transform.Cast<Transform>().ToArray())
            {
                if (sibling.name == "Handle" && sibling.gameObject != handleObject)
                    Object.DestroyImmediate(sibling.gameObject);
            }
            var handle = GetOrAdd<Image>(handleObject);
            handle.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UITheme/ButtonSatin-v2.png");
            handle.type = Image.Type.Sliced;
            handle.raycastTarget = false;

            var control = GetOrAdd<SwitchControl>(root);
            var serialized = new SerializedObject(control);
            serialized.FindProperty("_toggle").objectReferenceValue = toggle;
            serialized.FindProperty("_track").objectReferenceValue = track;
            serialized.FindProperty("_handle").objectReferenceValue = handle;
            serialized.FindProperty("_trackWidthRatio").floatValue = 0.62f;
            serialized.FindProperty("_trackHeightRatio").floatValue = 0.68f;
            serialized.FindProperty("_handleSizeRatio").floatValue = 0.8f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void StyleIconButton(Button button)
        {
            var back = AssetDatabase.LoadAssetAtPath<GameObject>(LoginPath)
                .GetComponentsInChildren<Button>(true).First(item => item.name == "BackButton");
            button.colors = back.colors;
            button.GetComponent<Image>().color = Color.white;
        }

        internal static void StyleCloseButton(Button button)
        {
            StyleIconButton(button);
            var image = button.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UITheme/CloseButton-v1.png");
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            foreach (var text in button.GetComponentsInChildren<TextMeshProUGUI>(true))
                text.gameObject.SetActive(false);
        }

        private static void ConfigureMsgBoxShortcuts()
        {
            EditPrefab(MsgBoxPath, root =>
            {
                var panel = root.GetComponent<MsgBox>();
                var confirm = Find(root, "ConfirmButton").GetComponent<Button>();
                var close = Find(root, "CloseButton").GetComponent<Button>();
                StyleCloseButton(close);
                ConfigureShortcutHandler(root, panel, confirm, close, null,
                    new Selectable[] { confirm, Find(root, "CancelButton").GetComponent<Button>(), close }, true);
            });
        }

        private static void ConfigureCanvas()
        {
            EditPrefab(CanvasPath, root =>
            {
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            });
        }

        private static void ConfigureEventSystem()
        {
            EditPrefab(EventSystemPath, root =>
            {
                var module = root.GetComponent<InputSystemUIInputModule>();
                var guard = GetOrAdd<PokerUiInputModuleGuard>(root);
                SetReferences(guard, new Dictionary<string, Object> { ["_inputModule"] = module });
            });
        }

        private static void RegisterPanels()
        {
            EditPrefab(UiManagerPath, root =>
            {
                var manager = root.GetComponent<UIManager>();
                var serialized = new SerializedObject(manager);
                var panels = serialized.FindProperty("_panels");
                AppendPanel(panels, AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPath));
                AppendPanel(panels, AssetDatabase.LoadAssetAtPath<GameObject>(ExchangePath));
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static void AppendPanel(SerializedProperty panels, GameObject prefab)
        {
            if (prefab == null)
                throw new InvalidOperationException("Panel prefab was not created before registration.");
            var type = prefab.GetComponent<IPanel>()?.GetType();
            for (int i = 0; i < panels.arraySize; i++)
            {
                var existing = panels.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (existing != null && existing.GetComponent<IPanel>()?.GetType() == type)
                    return;
            }
            panels.InsertArrayElementAtIndex(panels.arraySize);
            panels.GetArrayElementAtIndex(panels.arraySize - 1).objectReferenceValue = prefab;
        }

        private static void ConfigureShortcutHandler(
            GameObject root,
            BasePanel panel,
            Button confirm,
            Button back,
            TMP_InputField amount,
            Selectable[] focusOrder,
            bool confirmAlwaysUsesPrimary = false)
        {
            var handler = GetOrAdd<PanelShortcutHandler>(root);
            var serialized = new SerializedObject(handler);
            serialized.FindProperty("_panel").objectReferenceValue = panel;
            serialized.FindProperty("_confirmButton").objectReferenceValue = confirm;
            serialized.FindProperty("_backButton").objectReferenceValue = back;
            serialized.FindProperty("_amountInput").objectReferenceValue = amount;
            serialized.FindProperty("_confirmAlwaysUsesPrimary").boolValue = confirmAlwaysUsesPrimary;
            var focus = serialized.FindProperty("_focusOrder");
            focus.arraySize = focusOrder?.Length ?? 0;
            for (int i = 0; i < focus.arraySize; i++)
                focus.GetArrayElementAtIndex(i).objectReferenceValue = focusOrder[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DecorateTile(GameObject tile, string label, string spritePath)
        {
            SetText(tile, label);
            var icon = Find(tile, "Icon") ?? NewUiObject("Icon", tile.transform);
            var image = GetOrAdd<Image>(icon);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            image.preserveAspect = true;
            image.raycastTarget = false;
            var rect = icon.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.25f, 0.32f);
            rect.anchorMax = new Vector2(0.75f, 0.88f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = tile.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.fontSize = 28f;
                var textRect = text.rectTransform;
                textRect.anchorMin = new Vector2(0.05f, 0.05f);
                textRect.anchorMax = new Vector2(0.95f, 0.28f);
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            }
        }

        private static GameObject EnsureSelectionMarker(GameObject button)
        {
            var marker = Find(button, "Selection") ?? NewUiObject("Selection", button.transform);
            var image = GetOrAdd<Image>(marker);
            image.color = Gold;
            image.raycastTarget = false;
            var rect = marker.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.15f, 0f);
            rect.anchorMax = new Vector2(0.85f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 8f);
            marker.transform.SetAsLastSibling();
            return marker;
        }

        private static void AddCurrencyIcon(GameObject row, string spritePath)
        {
            var icon = Find(row, "CurrencyIcon") ?? NewUiObject("CurrencyIcon", row.transform);
            var image = GetOrAdd<Image>(icon);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            image.preserveAspect = true;
            image.raycastTarget = false;
            var rect = icon.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.72f, 0.18f);
            rect.anchorMax = new Vector2(0.82f, 0.82f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CloneText(GameObject sourceRoot, Transform parent, string name)
        {
            var source = sourceRoot.GetComponentsInChildren<TextMeshProUGUI>(true).First();
            var clone = Object.Instantiate(source.gameObject, parent);
            clone.name = name;
            return clone.GetComponent<TextMeshProUGUI>();
        }

        private static LayoutElement AddLayout(GameObject target, float preferredHeight)
        {
            var layout = GetOrAdd<LayoutElement>(target);
            layout.preferredHeight = preferredHeight;
            layout.minHeight = preferredHeight;
            return layout;
        }

        private static void SetObjectArray(SerializedProperty property, IEnumerable<Object> values)
        {
            var items = values.Where(value => value != null).ToArray();
            property.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void SetReferences(Component component, IReadOnlyDictionary<string, Object> references)
        {
            var serialized = new SerializedObject(component);
            foreach (var pair in references)
            {
                var property = serialized.FindProperty(pair.Key);
                if (property == null)
                    throw new InvalidOperationException($"Missing serialized field {component.GetType().Name}.{pair.Key}");
                property.objectReferenceValue = pair.Value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetText(GameObject target, string text)
        {
            if (target == null)
                return;
            var label = target.GetComponent<TextMeshProUGUI>() ?? target.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = text;
        }

        private static GameObject FindDirectOrDeep(GameObject root, string name)
        {
            var direct = root.transform.Find(name);
            return direct == null ? Find(root, name) : direct.gameObject;
        }

        private static GameObject FindPath(GameObject root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;
            var transform = root.transform.Find(path);
            return transform == null ? null : transform.gameObject;
        }

        private static GameObject Find(GameObject root, string name)
        {
            if (root.name == name)
                return root;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                    return transform.gameObject;
            }
            return null;
        }

        private static GameObject NewUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            return target.GetComponent<T>() ?? target.AddComponent<T>();
        }

        private static void RemoveComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component != null)
                Object.DestroyImmediate(component);
        }

        private static void DestroyNamedExcept(GameObject root, IReadOnlyCollection<GameObject> except, params string[] names)
        {
            foreach (string name in names)
            {
                var target = Find(root, name);
                if (target != null && !except.Contains(target))
                    Object.DestroyImmediate(target);
            }
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static GameObject CloneNodeFromPrefab(string prefabPath, string nodeName, Transform parent)
        {
            var sourceRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var source = Find(sourceRoot, nodeName) ?? throw new InvalidOperationException($"Missing {nodeName} in {prefabPath}");
                return Object.Instantiate(source, parent);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
        }

        private static void ClonePrefab(string sourcePath, string destinationPath, string rootName)
        {
            var source = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                var clone = Object.Instantiate(source);
                clone.name = rootName;
                PrefabUtility.SaveAsPrefabAsset(clone, destinationPath);
                Object.DestroyImmediate(clone);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
            }
        }

        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = path[..path.LastIndexOf('/')];
            string name = path[(path.LastIndexOf('/') + 1)..];
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
