using System;
using System.Collections.Generic;
using System.Linq;
using Framework.UI;
using Game.UI.GamePanelElement;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RoomPlayerListItem = Game.UI.RoomPanelElement.PlayerListItem;

namespace Game.UI.EditorTools
{
    public static class GameAndRoomPanelPrefabEditor
    {
        private const string GameDirectory = "Assets/Prefabs/UI/Panels/GamePanel/";
        private const string RoomDirectory = "Assets/Prefabs/UI/Panels/RoomPanel/";
        private const string ThemeDirectory = "Assets/Sprites/UITheme/";
        private static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);
        private static readonly Color Ink = new Color(0.043f, 0.09f, 0.098f, 1f);
        private static readonly Color White = new Color(0.933f, 0.914f, 0.867f, 1f);
        private static readonly Color Gold = new Color(0.851f, 0.737f, 0.478f, 1f);

        [MenuItem("Poker/UI/Apply Game And Room Mobile UI")]
        public static void Apply()
        {
            ImportThemeIcons();
            EditPrefab(GameDirectory + "GamePanel.prefab", UpgradeGamePanel);
            foreach (var name in new[] { "Player", "Player4", "Player8", "Player16" })
                EditPrefab(GameDirectory + name + ".prefab", UpgradeGamePlayer);
            EditPrefab(GameDirectory + "RoundResultMsgBox.prefab", UpgradeRoundResult);
            EditPrefab(GameDirectory + "RoundResultListItem.prefab", UpgradeRoundResultItem);
            EditPrefab(RoomDirectory + "RoomPanel.prefab", UpgradeRoomPanel);
            EditPrefab(RoomDirectory + "Player.prefab", UpgradeRoomPlayer);
            AssetDatabase.SaveAssets();
            Debug.Log("Applied GamePanel, RoomPanel and round-result mobile UI updates.");
        }

        [MenuItem("Poker/UI/Remove Legacy Game Result UI")]
        public static void RemoveLegacyGameResultUI()
        {
            const string managerPath = "Assets/Prefabs/Manager/UIManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(managerPath);
            try
            {
                var manager = root.GetComponent<UIManager>();
                var serialized = new SerializedObject(manager);
                var panels = serialized.FindProperty("_panels");
                for (int index = panels.arraySize - 1; index >= 0; index--)
                {
                    var value = panels.GetArrayElementAtIndex(index).objectReferenceValue;
                    var path = value == null ? string.Empty : AssetDatabase.GetAssetPath(value);
                    if (path.EndsWith("/GameResultMsgBox.prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        int previousSize = panels.arraySize;
                        panels.DeleteArrayElementAtIndex(index);
                        if (panels.arraySize == previousSize)
                            panels.DeleteArrayElementAtIndex(index);
                    }
                }
                for (int index = 0; index < panels.arraySize; index++)
                {
                    if (panels.GetArrayElementAtIndex(index).objectReferenceValue == null)
                        throw new InvalidOperationException($"UIManager _panels contains a null entry at index {index}.");
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, managerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (var path in new[]
            {
                GameDirectory + "GameResultMsgBox.prefab",
                GameDirectory + "GameResultListItem.prefab",
                "Assets/Scripts/UI/GamePanel/GameResultMsgBox.cs",
                "Assets/Scripts/UI/GamePanel/GameResultListItem.cs",
            })
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null && !AssetDatabase.DeleteAsset(path))
                    throw new InvalidOperationException("Could not delete legacy UI asset: " + path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Removed the legacy full-game result UI and its UIManager registration.");
        }

        private static void UpgradeGamePanel(GameObject root)
        {
            var panel = root.GetComponent<GamePanel>();
            var panelData = new SerializedObject(panel);
            var playerRegion = Reference<RectTransform>(panelData, "_playerRegion");
            var tableRegion = Reference<RectTransform>(panelData, "_tableRegion");
            var playerList = Reference<Transform>(panelData, "_playerList");
            var operation = Reference<OperationArea>(panelData, "_operationArea");
            var leave = Reference<Button>(panelData, "_leaveButton");
            var oldSettings = Reference<Button>(panelData, "_settingsButton");
            var oldAccount = Reference<TextMeshProUGUI>(panelData, "_accountStatus");
            var title = Reference<TextMeshProUGUI>(panelData, "_title");
            var tableStatus = Reference<TextMeshProUGUI>(panelData, "_tableStatus");
            var countdownStatus = Reference<TextMeshProUGUI>(panelData, "_countdownStatus");
            var tableActions = root.transform.Find("TableActions") as RectTransform;
            var localHand = operation.transform.Find("Hole") as RectTransform;
            var community = FindRect(operation.transform, "Community");
            var localName = FindRect(operation.transform, "Name");
            var localChips = FindRect(operation.transform, "Chips");
            var operationData = new SerializedObject(operation);
            var holeCards = References<PokerElement>(operationData, "_hole")
                .Select(card => card.transform as RectTransform).Where(rect => rect != null).ToArray();
            var communityCards = References<PokerElement>(operationData, "_community")
                .Select(card => card.transform as RectTransform).Where(rect => rect != null).ToArray();
            var font = root.GetComponentInChildren<TMP_Text>(true).font;

            var playerHeading = root.transform.Find("ThemePlayerHeading");
            if (playerHeading != null)
                playerHeading.gameObject.SetActive(false);
            var localHandTitle = localHand == null ? null : localHand.Find("Title");
            if (localHandTitle != null)
                localHandTitle.gameObject.SetActive(false);
            if (localName != null)
            {
                if (localName.parent != operation.transform)
                    localName.SetParent(operation.transform, false);
                localName.gameObject.SetActive(false);
            }
            if (localChips != null)
                localChips.gameObject.SetActive(false);
            if (tableActions != null)
            {
                foreach (var button in tableActions.GetComponentsInChildren<Button>(true))
                    StyleRoundedButton(button, new Color(0.13f, 0.24f, 0.22f, 1f));
            }

            var utility = ResetRect(root.transform, "UtilityButtons");
            Anchor(utility, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -18f), new Vector2(410f, 100f));
            var utilityLayout = utility.gameObject.AddComponent<HorizontalLayoutGroup>();
            utilityLayout.spacing = 12f;
            utilityLayout.childControlWidth = utilityLayout.childControlHeight = false;
            utilityLayout.childForceExpandWidth = utilityLayout.childForceExpandHeight = false;
            utilityLayout.childAlignment = TextAnchor.UpperRight;

            if (oldSettings != null)
                oldSettings.gameObject.SetActive(false);
            var settings = IconButton("SettingsButton", utility, Sprite("SettingsButton-v1"), "设置", font);
            var tasks = IconButton("DailyTasksButton", utility, Sprite("TasksButton-v1"), "任务", font);
            var exchange = IconButton("ExchangeButton", utility, Sprite("ExchangeButton-v1"), "兑换", font);
            var history = IconButton("HistoryButton", utility, Sprite("HistoryButton-v1"), "上手结果", font);
            history.gameObject.SetActive(false);
            SetReference(panel, "_settingsButton", settings);

            if (oldAccount != null)
                oldAccount.gameObject.SetActive(false);
            var wallet = ResetRect(root.transform, "WalletStatus");
            Anchor(wallet, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(480f, 54f));
            var dollars = Currency(wallet, "Dollars", Sprite("Dollars-v1"), font, -118f);
            var chips = Currency(wallet, "Chips", Sprite("Chips-v1"), font, 118f);

            var spectatorHeader = ResetRect(playerRegion, "SpectatorHeader");
            Stretch(spectatorHeader, new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -68f),
                new Vector2(-18f, 0f));
            var spectatorCount = Label("SpectatorCount", spectatorHeader, "观战 0", font, 24f, White,
                TextAlignmentOptions.MidlineLeft);
            Stretch(spectatorCount.rectTransform, Vector2.zero, new Vector2(0.66f, 1f), Vector2.zero, Vector2.zero);
            var switchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Shared/SwitchControl.prefab");
            if (switchPrefab == null)
                throw new InvalidOperationException("SwitchControl.prefab must be created by core UI first.");
            var switchObject = (GameObject)PrefabUtility.InstantiatePrefab(switchPrefab, spectatorHeader);
            switchObject.name = "ShowSpectatorsSwitch";
            var switchRect = (RectTransform)switchObject.transform;
            Anchor(switchRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-4f, 0f), new Vector2(118f, 52f));
            var spectatorSwitch = switchObject.GetComponent<SwitchControl>();
            spectatorSwitch.SetIsOnWithoutNotify(false);
            var listRect = playerList as RectTransform;
            if (listRect != null && listRect.parent is RectTransform viewport && viewport.parent is RectTransform scroll)
                viewport.offsetMax = new Vector2(viewport.offsetMax.x, -76f);

            // Existing operation controls are moved into this dock on the first run. Reuse it on
            // later runs so their serialized OperationArea references are never destroyed.
            var actionDock = GetOrCreateRect(root.transform, "MobileActionDock");
            Stretch(actionDock, new Vector2(0.47f, 0.02f), new Vector2(0.985f, 0.28f), Vector2.zero, Vector2.zero);
            var mobile = ConfigureOperations(operation, actionDock, font);

            var shortcuts = GetOrAdd<PanelShortcutHandler>(root);
            shortcuts.SetBackButton(leave);
            shortcuts.SetConfirmButton(Reference<Button>(new SerializedObject(operation), "_betOrRaiseButton"));
            shortcuts.SetAmountInput(Reference<TMP_InputField>(new SerializedObject(operation), "_betInput"));
            shortcuts.SetFocusOrder(Reference<TMP_InputField>(new SerializedObject(operation), "_betInput"));

            var controls = GetOrAdd<GamePanelControls>(root);
            SetReference(controls, "_panel", panel);
            SetReference(controls, "_spectatorSwitch", spectatorSwitch);
            SetReference(controls, "_spectatorCount", spectatorCount);
            SetReference(controls, "_dailyTasksButton", tasks);
            SetReference(controls, "_exchangeButton", exchange);
            SetReference(controls, "_historyButton", history);
            SetReference(controls, "_dollars", dollars);
            SetReference(controls, "_chips", chips);
            SetReference(controls, "_shortcuts", shortcuts);
            SetReference(controls, "_mobileOperations", mobile);
            SetReference(panel, "_controls", controls);
            RepairLandscapeLayout(root);
        }

        internal static void RepairLandscapeLayout(GameObject root)
        {
            var table = root.transform.Find("TableRegion");
            Stretch((RectTransform)table.Find("ThemeActionDock"), new Vector2(0f, 0.035f),
                new Vector2(1f, 0.32f), Vector2.zero, Vector2.zero);
            var dock = (RectTransform)root.transform.Find("MobileActionDock");
            Stretch(dock, new Vector2(0.49f, 0.055f), new Vector2(0.968f, 0.29f), Vector2.zero, Vector2.zero);
            var hand = (RectTransform)table.Find("OperationArea/Hole");
            Stretch(hand, new Vector2(0.015f, 0.065f), new Vector2(0.25f, 0.295f), Vector2.zero, Vector2.zero);
            var cards = hand.GetComponentsInChildren<PokerElement>(true);
            for (int i = 0; i < cards.Length; i++)
                Anchor((RectTransform)cards[i].transform, new Vector2(i == 0 ? 0.25f : 0.75f, 0.5f),
                    new Vector2(i == 0 ? 0.25f : 0.75f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(124f, 186f));
            Stretch((RectTransform)dock.Find("InputField (TMP)"), new Vector2(0.15f, 0.54f),
                new Vector2(0.42f, 0.88f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
            Stretch((RectTransform)dock.Find("DecreaseBlindButton"), new Vector2(0.01f, 0.54f),
                new Vector2(0.14f, 0.88f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)dock.Find("IncreaseBlindButton"), new Vector2(0.43f, 0.54f),
                new Vector2(0.56f, 0.88f), Vector2.zero, Vector2.zero);
            var players = (RectTransform)root.transform.Find("PlayerList");
            var viewport = (RectTransform)players.Find("Viewport");
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, -146f);
            foreach (var name in new[] { "TableStatus", "LeaveStatus" })
            {
                var status = (RectTransform)root.transform.Find(name);
                Anchor(status, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                    new Vector2(80f, -180f), new Vector2(440f, 52f));
                status.GetComponent<TextMeshProUGUI>().fontSize = 24f;
            }
        }

        private static OperationAreaMobileControls ConfigureOperations(OperationArea operation, RectTransform dock, TMP_FontAsset font)
        {
            var data = new SerializedObject(operation);
            var check = Reference<Button>(data, "_checkButton");
            var raise = Reference<Button>(data, "_betOrRaiseButton");
            var call = Reference<Button>(data, "_callButton");
            var allIn = Reference<Button>(data, "_allInButton");
            var fold = Reference<Button>(data, "_foldButton");
            var input = Reference<TMP_InputField>(data, "_betInput");
            var hint = Reference<TextMeshProUGUI>(data, "_betHint");
            var raiseTitle = Reference<TextMeshProUGUI>(data, "_betOrRaiseTitle");
            foreach (var transform in new[] { check.transform, raise.transform, call.transform, allIn.transform, fold.transform, input.transform })
                transform.SetParent(dock, false);

            ActionButton(check, 0.01f, 0.245f, new Color(0.08f, 0.34f, 0.26f, 1f));
            ActionButton(call, 0.01f, 0.245f, new Color(0.08f, 0.34f, 0.26f, 1f));
            ActionButton(raise, 0.255f, 0.49f, new Color(0.48f, 0.34f, 0.12f, 1f));
            RemoveMask(raise);
            if (raiseTitle != null)
            {
                Stretch(raiseTitle.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(8f, 4f), new Vector2(-8f, -4f));
                raiseTitle.alignment = TextAlignmentOptions.Center;
                raiseTitle.margin = Vector4.zero;
                raiseTitle.textWrappingMode = TextWrappingModes.NoWrap;
            }
            ActionButton(allIn, 0.50f, 0.735f, new Color(0.45f, 0.24f, 0.10f, 1f));
            ActionButton(fold, 0.745f, 0.99f, new Color(0.38f, 0.10f, 0.12f, 1f));
            Stretch(input.GetComponent<RectTransform>(), new Vector2(0.01f, 0.48f), new Vector2(0.24f, 0.94f),
                new Vector2(8f, 4f), new Vector2(-8f, -4f));
            if (hint != null)
            {
                hint.transform.SetParent(dock, false);
                Stretch(hint.rectTransform, new Vector2(0.01f, 0.38f), new Vector2(0.99f, 0.49f),
                    new Vector2(8f, 0f), new Vector2(-8f, 0f));
                hint.fontSize = 18f;
            }

            var decrease = TextButton("DecreaseBlindButton", dock, "-1BB", font);
            var increase = TextButton("IncreaseBlindButton", dock, "+1BB", font);
            var minimum = TextButton("MinimumButton", dock, "最小", font);
            var halfPot = TextButton("HalfPotButton", dock, "1/2池", font);
            var pot = TextButton("PotButton", dock, "1池", font);
            QuickButton(minimum, 0.62f, 0.735f);
            QuickButton(halfPot, 0.745f, 0.86f);
            QuickButton(pot, 0.87f, 0.99f);

            var component = GetOrAdd<OperationAreaMobileControls>(operation.gameObject);
            SetReference(component, "_checkButton", check);
            SetReference(component, "_callButton", call);
            SetReference(component, "_raiseButton", raise);
            SetReference(component, "_allInButton", allIn);
            SetReference(component, "_foldButton", fold);
            SetReference(component, "_raiseInput", input);
            SetReference(component, "_decreaseBlindButton", decrease);
            SetReference(component, "_increaseBlindButton", increase);
            var callCost = Shortcut(call, font);
            callCost.name = "CallCost";
            Stretch(callCost.rectTransform, new Vector2(0f, -0.3f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            callCost.alignment = TextAlignmentOptions.Center;
            callCost.fontSize = 20f;
            SetReference(component, "_callCost", callCost);
            SetReference(component, "_minimumButton", minimum);
            SetReference(component, "_halfPotButton", halfPot);
            SetReference(component, "_potButton", pot);
            SetReference(component, "_checkShortcut", Shortcut(check, font));
            SetReference(component, "_callShortcut", Shortcut(call, font));
            SetReference(component, "_raiseShortcut", Shortcut(raise, font));
            SetReference(component, "_allInShortcut", Shortcut(allIn, font));
            SetReference(component, "_foldShortcut", Shortcut(fold, font));
            return component;
        }

        private static void UpgradeGamePlayer(GameObject root)
        {
            var player = root.GetComponent<PlayerListItem>();
            var data = new SerializedObject(player);
            var canvas = Reference<CanvasGroup>(data, "_canvasGroup") ?? GetOrAdd<CanvasGroup>(root);
            var time = Reference<TextMeshProUGUI>(data, "_timeText");
            var name = Reference<TextMeshProUGUI>(data, "_name");
            var chips = Reference<TextMeshProUGUI>(data, "_chipsText");
            var fold = Reference<OperationIcon>(data, "_foldIcon");
            var state = GetOrAdd<PlayerListItemStateView>(root);
            var oldEye = root.transform.Find("SpectatorEye");
            if (oldEye != null)
                UnityEngine.Object.DestroyImmediate(oldEye.gameObject);
            var eye = ResetRect(fold.transform.parent, "SpectatorEye");
            CopyLayout(fold.GetComponent<RectTransform>(), eye);
            var image = eye.gameObject.AddComponent<Image>();
            image.sprite = Sprite("SpectateButton-v1");
            image.preserveAspect = true;
            image.raycastTarget = false;
            eye.gameObject.SetActive(false);
            SetReference(state, "_canvasGroup", canvas);
            SetReference(state, "_time", time);
            SetReference(state, "_name", name);
            SetReference(state, "_chips", chips);
            SetReference(state, "_spectatorIcon", eye.gameObject);
            SetReference(state, "_foldIcon", fold.gameObject);
            SetObjectList(state, "_operationIcons", new[] { "_checkIcon", "_betIcon", "_raiseIcon", "_callIcon", "_allInIcon", "_foldIcon" }
                .Select(name => Reference<OperationIcon>(data, name)?.gameObject).Where(value => value != null).ToArray());
        }

        private static void UpgradeRoundResult(GameObject root)
        {
            var panel = root.GetComponent<RoundResultMsgBox>();
            var data = new SerializedObject(panel);
            var close = Reference<Button>(data, "_closeButton");
            Game.UI.Editor.CoreUiPrefabMigration.StyleCloseButton(close);
            var confirm = Reference<Button>(data, "_confirmButton");
            var font = root.GetComponentInChildren<TMP_Text>(true).font;
            var label = confirm.GetComponentInChildren<TextMeshProUGUI>(true);
            label.text = "确认并准备";
            var status = Label("ReadyStatus", confirm.transform.parent, "", font, 22f, Gold,
                TextAlignmentOptions.Center);
            Anchor(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 82f), new Vector2(420f, 38f));
            status.gameObject.SetActive(false);
            SetReference(panel, "_confirmButtonLabel", label);
            SetReference(panel, "_readyStatus", status);
            var shortcuts = GetOrAdd<PanelShortcutHandler>(root);
            shortcuts.SetConfirmButton(confirm);
            shortcuts.SetBackButton(close);
            SetBool(shortcuts, "_confirmAlwaysUsesPrimary", true);
        }

        private static void UpgradeRoundResultItem(GameObject root)
        {
            var item = root.GetComponent<RoundResultListItem>();
            var visual = GetOrCreateRect(root.transform, "ResponsiveVisual");
            Anchor(visual, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(336f, 324f));
            foreach (var child in root.transform.Cast<Transform>().Where(child => child != visual).ToArray())
                child.SetParent(visual, false);
            var button = GetOrAdd<Button>(root);
            button.targetGraphic = root.GetComponent<Image>();
            button.transition = Selectable.Transition.None;
            var font = root.GetComponentInChildren<TMP_Text>(true).font;
            var hint = Label("RevealHint", visual, "点击亮牌", font, 20f, Gold,
                TextAlignmentOptions.Center);
            Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 14f), new Vector2(220f, 34f));
            hint.gameObject.SetActive(false);
            SetReference(item, "_revealButton", button);
            SetReference(item, "_revealHint", hint);
        }

        private static void UpgradeRoomPanel(GameObject root)
        {
            var panel = root.GetComponent<RoomPanel>();
            var data = new SerializedObject(panel);
            var font = root.GetComponentInChildren<TMP_Text>(true).font;
            var back = Reference<Button>(data, "_back");
            var ready = Reference<Button>(data, "_ready");
            var cancelReady = Reference<Button>(data, "_cancelReady");
            var start = Reference<Button>(data, "_start");
            var title = Reference<TextMeshProUGUI>(data, "_title");
            var scroll = Reference<ScrollRect>(data, "_scroll");
            var roomStatus = Reference<TextMeshProUGUI>(data, "_roomStatus");
            var settingsHint = Reference<TextMeshProUGUI>(data, "_settingsHint");
            var playerContent = Reference<Transform>(data, "_playerList") as RectTransform;
            var inputs = new[]
            {
                Reference<TMP_InputField>(data, "_smallBlindChips"),
                Reference<TMP_InputField>(data, "_thinkingTime"),
                Reference<TMP_InputField>(data, "_interHandWait"),
                Reference<TMP_InputField>(data, "_maxPlayer"),
            };
            var settingRows = inputs.Select(input => input == null ? null : input.transform.parent as RectTransform)
                .ToArray();
            var settingLabels = settingRows.Select(row => row == null ? null : row.Find("Text (TMP)") as RectTransform)
                .ToArray();
            var settingsPanel = settingRows.FirstOrDefault(row => row != null)?.parent as RectTransform;
            var oldAccount = Reference<TextMeshProUGUI>(data, "_accountStatus");
            if (oldAccount != null)
                oldAccount.gameObject.SetActive(false);

            var utility = ResetRect(root.transform, "UtilityButtons");
            Anchor(utility, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-32f, -20f), new Vector2(300f, 100f));
            var layout = utility.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperRight;
            var tasks = IconButton("DailyTasksButton", utility, Sprite("TasksButton-v1"), "任务", font);
            var achievements = IconButton("AchievementsButton", utility,
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Achievements/Default.png"), "成就", font);
            var exchange = IconButton("ExchangeButton", utility, Sprite("ExchangeButton-v1"), "兑换", font);

            var participation = TextButton("ParticipationButton", root.transform, "转为观战", font);
            Anchor(participation.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(260f, 76f));
            var participationLabel = participation.GetComponentInChildren<TextMeshProUGUI>(true);
            participationLabel.fontSize = 28f;
            StyleRoundedButton(participation, new Color(0.13f, 0.30f, 0.25f, 1f));
            StyleRoundedButton(ready, new Color(0.08f, 0.34f, 0.26f, 1f));
            StyleRoundedButton(cancelReady, new Color(0.48f, 0.34f, 0.12f, 1f));
            StyleRoundedButton(start, new Color(0.48f, 0.34f, 0.12f, 1f));
            RemoveMask(participation);
            RemoveMask(ready);
            RemoveMask(cancelReady);
            RemoveMask(start);

            var wallet = ResetRect(root.transform, "WalletStatus");
            Anchor(wallet, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(480f, 54f));
            var dollars = Currency(wallet, "Dollars", Sprite("Dollars-v1"), font, -118f);
            var chips = Currency(wallet, "Chips", Sprite("Chips-v1"), font, 118f);

            var controls = GetOrAdd<RoomPanelControls>(root);
            SetReference(controls, "_panel", panel);
            SetReference(controls, "_participationButton", participation);
            SetReference(controls, "_participationLabel", participationLabel);
            SetReference(controls, "_readyButton", ready);
            SetReference(controls, "_cancelReadyButton", cancelReady);
            SetReference(controls, "_startButton", start);
            SetReference(controls, "_dailyTasksButton", tasks);
            SetReference(controls, "_achievementsButton", achievements);
            SetReference(controls, "_exchangeButton", exchange);
            SetReference(controls, "_dollars", dollars);
            SetReference(controls, "_chips", chips);

            var shortcuts = GetOrAdd<PanelShortcutHandler>(root);
            shortcuts.SetBackButton(back);
            shortcuts.SetFocusOrder(
                Reference<TMP_InputField>(data, "_smallBlindChips"),
                Reference<TMP_InputField>(data, "_thinkingTime"),
                Reference<TMP_InputField>(data, "_interHandWait"),
                Reference<TMP_InputField>(data, "_maxPlayer"));

        }

        private static void UpgradeRoomPlayer(GameObject root)
        {
            var item = root.GetComponent<RoomPlayerListItem>();
            var canvas = GetOrAdd<CanvasGroup>(root);
            var eye = ResetRect(root.transform, "SpectatorEye");
            Anchor(eye, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-22f, 0f), new Vector2(52f, 52f));
            var image = eye.gameObject.AddComponent<Image>();
            image.sprite = Sprite("SpectateButton-v1");
            image.preserveAspect = true;
            image.raycastTarget = false;
            eye.gameObject.SetActive(false);
            SetReference(item, "_canvasGroup", canvas);
            SetReference(item, "_spectatorIcon", eye.gameObject);
        }

        private static void ImportThemeIcons()
        {
            foreach (var name in new[]
            {
                "Dollars-v1", "Chips-v1", "ExchangeButton-v1", "TasksButton-v1",
                "HistoryButton-v1", "RoomsButton-v1", "LeaderboardButton-v1", "SpectateButton-v1",
            })
            {
                var path = ThemeDirectory + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("Missing UI icon: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
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

        private static T Reference<T>(SerializedObject serialized, string name) where T : UnityEngine.Object
        {
            var property = serialized.FindProperty(name);
            return property == null ? null : property.objectReferenceValue as T;
        }

        private static IEnumerable<T> References<T>(SerializedObject serialized, string name)
            where T : UnityEngine.Object
        {
            var property = serialized.FindProperty(name);
            if (property == null || !property.isArray)
                yield break;
            for (int index = 0; index < property.arraySize; index++)
            {
                if (property.GetArrayElementAtIndex(index).objectReferenceValue is T value)
                    yield return value;
            }
        }

        private static void SetReference(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(name) ?? throw new InvalidOperationException(
                target.GetType().Name + " is missing serialized field " + name + ".");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectList(UnityEngine.Object target, string name, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(name) ?? throw new InvalidOperationException(name);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string name, bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(name) ?? throw new InvalidOperationException(
                target.GetType().Name + " is missing serialized field " + name + ".");
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static RectTransform FindRect(Transform parent, string name)
        {
            return parent.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == name);
        }

        private static RectTransform ResetRect(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false);
            result.gameObject.layer = parent.gameObject.layer;
            return result;
        }

        private static RectTransform GetOrCreateRect(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
                return existing;
            var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false);
            result.gameObject.layer = parent.gameObject.layer;
            return result;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string text, TMP_FontAsset font,
            float size, Color color, TextAlignmentOptions alignment)
        {
            var label = ResetRect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static Button IconButton(string name, Transform parent, Sprite icon, string caption, TMP_FontAsset font)
        {
            if (icon == null)
                throw new InvalidOperationException("Missing icon for " + name + ".");
            var rect = ResetRect(parent, name);
            rect.sizeDelta = new Vector2(88f, 98f);
            rect.gameObject.AddComponent<LayoutElement>().preferredWidth = 88f;
            var target = rect.gameObject.AddComponent<Image>();
            target.color = Transparent;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            var iconImage = ResetRect(rect, "Icon").gameObject.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            Stretch(iconImage.rectTransform, new Vector2(0.12f, 0.25f), new Vector2(0.88f, 0.95f), Vector2.zero, Vector2.zero);
            var label = Label("Caption", rect, caption, font, 18f, White, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, Vector2.zero, new Vector2(1f, 0.24f), Vector2.zero, Vector2.zero);
            return button;
        }

        private static TextMeshProUGUI Currency(Transform parent, string name, Sprite icon, TMP_FontAsset font, float x)
        {
            var group = ResetRect(parent, name);
            Anchor(group, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, 0f), new Vector2(220f, 50f));
            var image = ResetRect(group, "Icon").gameObject.AddComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            Anchor(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0f), new Vector2(42f, 42f));
            var label = Label("Amount", group, "$0", font, 23f, Gold, TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(52f, 0f), Vector2.zero);
            return label;
        }

        private static Button TextButton(string name, Transform parent, string text, TMP_FontAsset font)
        {
            var rect = ResetRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ThemeDirectory + "Surface.png");
            image.type = Image.Type.Sliced;
            image.color = new Color(0.13f, 0.24f, 0.22f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = Label("Label", rect, text, font, 22f, White, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            return button;
        }

        private static TextMeshProUGUI Shortcut(Button button, TMP_FontAsset font)
        {
            var label = Label("Shortcut", button.transform, "", font, 16f, new Color(0.72f, 0.80f, 0.75f, 1f),
                TextAlignmentOptions.BottomRight);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            return label;
        }

        private static void ActionButton(Button button, float minX, float maxX, Color color)
        {
            StyleRoundedButton(button, color);
            Stretch(button.GetComponent<RectTransform>(), new Vector2(minX, 0.02f), new Vector2(maxX, 0.36f),
                new Vector2(5f, 4f), new Vector2(-5f, -4f));
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.fontSize = Mathf.Max(text.fontSize, 25f);
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        private static void StyleRoundedButton(Button button, Color color)
        {
            if (button == null)
                return;
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ThemeDirectory + "Surface.png");
                image.type = Image.Type.Sliced;
                image.color = Color.white;
                button.targetGraphic = image;
            }
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Lighten(color, 0.12f);
            colors.pressedColor = Darken(color, 0.22f);
            colors.selectedColor = Lighten(color, 0.08f);
            colors.disabledColor = new Color(color.r * 0.48f, color.g * 0.48f, color.b * 0.48f, 0.82f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
                text.color = White;
            var squareOverlay = button.transform.Find("ThemeButtonTexture");
            if (squareOverlay != null)
                squareOverlay.gameObject.SetActive(false);
        }

        private static Color Lighten(Color color, float amount) => new Color(
            Mathf.Lerp(color.r, 1f, amount), Mathf.Lerp(color.g, 1f, amount),
            Mathf.Lerp(color.b, 1f, amount), color.a);

        private static Color Darken(Color color, float amount) => new Color(
            color.r * (1f - amount), color.g * (1f - amount), color.b * (1f - amount), color.a);

        private static void RemoveMask(Button button)
        {
            if (button == null)
                return;
            foreach (var mask in button.GetComponents<Mask>())
                UnityEngine.Object.DestroyImmediate(mask);
        }

        private static void QuickButton(Button button, float minX, float maxX)
        {
            Stretch(button.GetComponent<RectTransform>(), new Vector2(minX, 0.56f), new Vector2(maxX, 0.90f),
                new Vector2(4f, 2f), new Vector2(-4f, -2f));
        }

        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ThemeDirectory + name + ".png");

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void CopyLayout(RectTransform source, RectTransform destination)
        {
            if (source == null || destination == null)
                return;
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
        }
    }
}
