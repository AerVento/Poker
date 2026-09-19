using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    public enum PokerShortcutAction
    {
        Back,
        Confirm,
        NextField,
        PreviousField,
        Fold,
        CheckOrCall,
        FocusRaiseAmount,
        AllIn,
    }

    public enum ShortcutRebindResult
    {
        Success,
        Cancelled,
        Conflict,
        Busy,
        InvalidControl,
    }

    public readonly struct ShortcutRebindOutcome
    {
        public ShortcutRebindOutcome(ShortcutRebindResult result, PokerShortcutAction conflictingAction = default)
        {
            Result = result;
            ConflictingAction = conflictingAction;
        }

        public ShortcutRebindResult Result { get; }

        public PokerShortcutAction ConflictingAction { get; }
    }

    public static class PokerShortcutBindings
    {
        private const string LegacyBindingOverridesKey = "Poker.Shortcuts.BindingOverrides.v1";
        private const string BindingPathKeyPrefix = "Poker.Shortcuts.Path.v1.";
        private const string BindingShiftKeyPrefix = "Poker.Shortcuts.Shift.v1.";

        private sealed class BindingDefinition
        {
            public BindingDefinition(PokerShortcutAction shortcut, string actionName, string defaultPath, bool requiresShift)
            {
                Shortcut = shortcut;
                ActionName = actionName;
                DefaultPath = defaultPath;
                RequiresShift = requiresShift;
            }

            public PokerShortcutAction Shortcut { get; }
            public string ActionName { get; }
            public string DefaultPath { get; }
            public bool RequiresShift { get; }
        }

        private static readonly BindingDefinition[] Definitions =
        {
            new(PokerShortcutAction.Back, "Back", "<Keyboard>/escape", false),
            new(PokerShortcutAction.Confirm, "Confirm", "<Keyboard>/enter", false),
            new(PokerShortcutAction.NextField, "NextField", "<Keyboard>/tab", false),
            new(PokerShortcutAction.PreviousField, "PreviousField", "<Keyboard>/tab", true),
            new(PokerShortcutAction.Fold, "Fold", "<Keyboard>/f", false),
            new(PokerShortcutAction.CheckOrCall, "CheckOrCall", "<Keyboard>/c", false),
            new(PokerShortcutAction.FocusRaiseAmount, "FocusRaiseAmount", "<Keyboard>/r", false),
            new(PokerShortcutAction.AllIn, "AllIn", "<Keyboard>/a", true),
        };

        private static readonly Dictionary<PokerShortcutAction, BindingDefinition> DefinitionByShortcut = new();
        private static readonly Dictionary<PokerShortcutAction, InputAction> ActionByShortcut = new();
        private static readonly Dictionary<PokerShortcutAction, bool> RequiresShiftByShortcut = new();

        private static InputActionMap _actionMap;
        private static InputActionRebindingExtensions.RebindingOperation _rebindOperation;
        private static PokerShortcutAction _rebindShortcut;
        private static string _previousOverridePath;
        private static bool _previousRequiresShift;
        private static bool _candidateRequiresShift;
        private static Action<ShortcutRebindOutcome> _rebindCompleted;
        private static bool _initialized;
        private static int _consumedFrame = -1;

        public static event Action<PokerShortcutAction> ShortcutPerformed;
        public static event Action BindingChanged;

        public static bool IsRebinding => _rebindOperation != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            _rebindOperation?.Dispose();
            _rebindOperation = null;
            _actionMap?.Dispose();
            _actionMap = null;
            DefinitionByShortcut.Clear();
            ActionByShortcut.Clear();
            RequiresShiftByShortcut.Clear();
            ShortcutPerformed = null;
            BindingChanged = null;
            _rebindCompleted = null;
            _consumedFrame = -1;
            _initialized = false;
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            _actionMap = new InputActionMap("PokerShortcuts");
            foreach (var definition in Definitions)
            {
                DefinitionByShortcut.Add(definition.Shortcut, definition);
                RequiresShiftByShortcut.Add(definition.Shortcut, definition.RequiresShift);
                var shortcut = definition.Shortcut;
                var action = _actionMap.AddAction(definition.ActionName, InputActionType.Button, definition.DefaultPath);
                action.performed += _ => HandlePerformed(shortcut);
                ActionByShortcut.Add(shortcut, action);
            }

            LoadOverrides();
            _actionMap.Enable();
        }

        public static string GetDisplayName(PokerShortcutAction shortcut)
        {
            EnsureInitialized();
            var action = ActionByShortcut[shortcut];
            var displayName = action.GetBindingDisplayString(0,
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = InputControlPath.ToHumanReadableString(action.bindings[0].effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
            return RequiresShiftByShortcut[shortcut] ? $"Shift + {displayName}" : displayName;
        }

        public static string GetActionLabel(PokerShortcutAction shortcut)
        {
            return shortcut switch
            {
                PokerShortcutAction.Back => "返回",
                PokerShortcutAction.Confirm => "确认",
                PokerShortcutAction.NextField => "下一项",
                PokerShortcutAction.PreviousField => "上一项",
                PokerShortcutAction.Fold => "弃牌",
                PokerShortcutAction.CheckOrCall => "过牌或跟注",
                PokerShortcutAction.FocusRaiseAmount => "输入加注额",
                PokerShortcutAction.AllIn => "全下",
                _ => shortcut.ToString(),
            };
        }

        public static bool TryBeginInteractiveRebind(
            PokerShortcutAction shortcut,
            Action<ShortcutRebindOutcome> completed)
        {
            EnsureInitialized();
            if (_rebindOperation != null)
            {
                completed?.Invoke(new ShortcutRebindOutcome(ShortcutRebindResult.Busy));
                return false;
            }

            if (shortcut == PokerShortcutAction.Back)
            {
                completed?.Invoke(new ShortcutRebindOutcome(ShortcutRebindResult.InvalidControl));
                return false;
            }
            var action = ActionByShortcut[shortcut];
            _rebindShortcut = shortcut;
            _previousOverridePath = action.bindings[0].overridePath;
            _previousRequiresShift = RequiresShiftByShortcut[shortcut];
            _rebindCompleted = completed;
            action.Disable();

            _rebindOperation = action.PerformInteractiveRebinding(0)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsExcluding("<Keyboard>/leftShift")
                .WithControlsExcluding("<Keyboard>/rightShift")
                .WithControlsExcluding("<Keyboard>/leftCtrl")
                .WithControlsExcluding("<Keyboard>/rightCtrl")
                .WithControlsExcluding("<Keyboard>/leftAlt")
                .WithControlsExcluding("<Keyboard>/rightAlt")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ => FinishRebind(ShortcutRebindResult.Cancelled))
                .OnComplete(_ => ValidateCompletedRebind());
            _rebindOperation.Start();
            return true;
        }

        public static void CancelInteractiveRebind()
        {
            _rebindOperation?.Cancel();
        }

        public static void ResetDefaults()
        {
            EnsureInitialized();
            CancelInteractiveRebind();
            foreach (var pair in ActionByShortcut)
            {
                pair.Value.RemoveAllBindingOverrides();
                RequiresShiftByShortcut[pair.Key] = DefinitionByShortcut[pair.Key].RequiresShift;
                PlayerPrefs.DeleteKey(GetBindingPathKey(pair.Key));
                PlayerPrefs.DeleteKey(GetBindingShiftKey(pair.Key));
            }
            PlayerPrefs.DeleteKey(LegacyBindingOverridesKey);
            PlayerPrefs.Save();
            BindingChanged?.Invoke();
        }

        public static bool TryConsumeCurrentFrame()
        {
            if (_consumedFrame == Time.frameCount)
                return false;
            _consumedFrame = Time.frameCount;
            return true;
        }

        private static void HandlePerformed(PokerShortcutAction shortcut)
        {
            if (_rebindOperation != null || !ModifiersMatch(shortcut))
                return;
            ShortcutPerformed?.Invoke(shortcut);
        }

        private static bool ModifiersMatch(PokerShortcutAction shortcut)
        {
            var keyboard = Keyboard.current;
            if (IsGameplayShortcut(shortcut) && keyboard != null &&
                (keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed ||
                 keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed))
                return false;

            bool shiftPressed = keyboard != null && keyboard.shiftKey.isPressed;
            return RequiresShiftByShortcut[shortcut] ? shiftPressed : !shiftPressed;
        }

        private static bool IsGameplayShortcut(PokerShortcutAction shortcut)
        {
            return shortcut == PokerShortcutAction.Fold ||
                shortcut == PokerShortcutAction.CheckOrCall ||
                shortcut == PokerShortcutAction.FocusRaiseAmount ||
                shortcut == PokerShortcutAction.AllIn;
        }

        private static void ValidateCompletedRebind()
        {
            var action = ActionByShortcut[_rebindShortcut];
            var path = action.bindings[0].effectivePath;
            _candidateRequiresShift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            if (string.IsNullOrWhiteSpace(path) || IsModifierPath(path))
            {
                RestorePreviousOverride(action);
                FinishRebind(ShortcutRebindResult.InvalidControl);
                return;
            }

            foreach (var pair in ActionByShortcut)
            {
                if (pair.Key == _rebindShortcut ||
                    RequiresShiftByShortcut[pair.Key] != _candidateRequiresShift)
                    continue;
                if (!string.Equals(pair.Value.bindings[0].effectivePath, path, StringComparison.OrdinalIgnoreCase))
                    continue;

                RestorePreviousOverride(action);
                FinishRebind(ShortcutRebindResult.Conflict, pair.Key);
                return;
            }

            RequiresShiftByShortcut[_rebindShortcut] = _candidateRequiresShift;
            SaveOverrides();
            FinishRebind(ShortcutRebindResult.Success);
        }

        private static void FinishRebind(
            ShortcutRebindResult result,
            PokerShortcutAction conflictingAction = default)
        {
            var operation = _rebindOperation;
            _rebindOperation = null;
            operation?.Dispose();
            ActionByShortcut[_rebindShortcut].Enable();
            var completed = _rebindCompleted;
            _rebindCompleted = null;
            _previousOverridePath = null;
            if (result == ShortcutRebindResult.Success)
                BindingChanged?.Invoke();
            completed?.Invoke(new ShortcutRebindOutcome(result, conflictingAction));
        }

        private static void RestorePreviousOverride(InputAction action)
        {
            RequiresShiftByShortcut[_rebindShortcut] = _previousRequiresShift;
            if (string.IsNullOrEmpty(_previousOverridePath))
                action.RemoveBindingOverride(0);
            else
                action.ApplyBindingOverride(0, _previousOverridePath);
        }

        private static bool IsModifierPath(string path)
        {
            return path.EndsWith("Shift", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("Alt", StringComparison.OrdinalIgnoreCase);
        }

        private static void LoadOverrides()
        {
            var desiredPaths = new Dictionary<PokerShortcutAction, string>();
            var desiredShift = new Dictionary<PokerShortcutAction, bool>();
            var storedPaths = new HashSet<PokerShortcutAction>();
            foreach (var definition in Definitions)
            {
                if (definition.Shortcut == PokerShortcutAction.Back)
                {
                    PlayerPrefs.DeleteKey(GetBindingPathKey(definition.Shortcut));
                    PlayerPrefs.DeleteKey(GetBindingShiftKey(definition.Shortcut));
                }
                string key = GetBindingPathKey(definition.Shortcut);
                string storedPath = PlayerPrefs.GetString(key, string.Empty);
                bool hasStoredPath = !string.IsNullOrWhiteSpace(storedPath);
                desiredPaths[definition.Shortcut] = hasStoredPath ? storedPath : definition.DefaultPath;
                desiredShift[definition.Shortcut] = hasStoredPath && PlayerPrefs.HasKey(GetBindingShiftKey(definition.Shortcut))
                    ? PlayerPrefs.GetInt(GetBindingShiftKey(definition.Shortcut)) != 0
                    : definition.RequiresShift;
                if (hasStoredPath)
                    storedPaths.Add(definition.Shortcut);
            }

            foreach (var definition in Definitions)
            {
                if (!storedPaths.Contains(definition.Shortcut))
                    continue;

                string path = desiredPaths[definition.Shortcut];
                bool valid = IsValidKeyboardPath(path);
                if (valid)
                {
                    foreach (var other in Definitions)
                    {
                        if (other.Shortcut == definition.Shortcut ||
                            desiredShift[other.Shortcut] != desiredShift[definition.Shortcut])
                            continue;
                        if (string.Equals(path, desiredPaths[other.Shortcut], StringComparison.OrdinalIgnoreCase))
                        {
                            valid = false;
                            break;
                        }
                    }
                }

                if (valid)
                {
                    ActionByShortcut[definition.Shortcut].ApplyBindingOverride(0, path);
                    RequiresShiftByShortcut[definition.Shortcut] = desiredShift[definition.Shortcut];
                }
                else
                {
                    PlayerPrefs.DeleteKey(GetBindingPathKey(definition.Shortcut));
                    PlayerPrefs.DeleteKey(GetBindingShiftKey(definition.Shortcut));
                    Debug.LogWarning($"Ignoring invalid or conflicting shortcut binding for {definition.Shortcut}.");
                }
            }

            PlayerPrefs.DeleteKey(LegacyBindingOverridesKey);
        }

        private static void SaveOverrides()
        {
            foreach (var pair in ActionByShortcut)
            {
                PlayerPrefs.SetString(GetBindingPathKey(pair.Key), pair.Value.bindings[0].effectivePath);
                PlayerPrefs.SetInt(GetBindingShiftKey(pair.Key), RequiresShiftByShortcut[pair.Key] ? 1 : 0);
            }
            PlayerPrefs.Save();
        }

        private static string GetBindingPathKey(PokerShortcutAction shortcut)
        {
            return BindingPathKeyPrefix + shortcut;
        }

        private static string GetBindingShiftKey(PokerShortcutAction shortcut)
        {
            return BindingShiftKeyPrefix + shortcut;
        }

        private static bool IsValidKeyboardPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) &&
                !IsModifierPath(path);
        }
    }
}
