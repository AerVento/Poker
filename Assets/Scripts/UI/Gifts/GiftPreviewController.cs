using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Content;
using Game.Network;
using Game.SO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Gifts
{
    /// <summary>Offline preview of the gifts in Configs/Items/Gifts.json, using the same gift definitions as the table.</summary>
    public sealed class GiftPreviewController : MonoBehaviour
    {
        [SerializeField] private bool _useRemoteResources;
        private GiftEffectSO _effect;
        private CancellationTokenSource _selectionCancellation;
        private RemoteAssets.Lease<GiftEffectSO> _resources;
        [SerializeField] private RectTransform _effectRoot;
        [SerializeField] private RectTransform _choices;
        [SerializeField] private Button _choicePrefab;
        [SerializeField] private Button[] _targets;
        [SerializeField] private RectTransform _sender;
        [SerializeField] private Button _play;
        [SerializeField] private Button _burst;
        [SerializeField] private Button _clear;
        [SerializeField] private Toggle _loop;
        [SerializeField] private Toggle _sound;
        [SerializeField] private Slider _timeline;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private TMP_Text _targetLabel;
        private readonly List<(GiftDefinition Gift, ItemDefinition Item)> _entries = new List<(GiftDefinition, ItemDefinition)>();
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<GiftEffect> _active = new List<GiftEffect>();
        private readonly List<float> _started = new List<float>();
        private int _selected;
        private int _target = 1;
        private int _queued;
        private float _next;
        private bool _scrubbing;
        private GiftEffect _pose;
        private float _lastStarted;

        private void OnEnable()
        {
            _entries.Clear();
            var definitions = Definitions();
            foreach (var gift in definitions?.Gifts ?? Array.Empty<GiftDefinition>())
            {
                var item = definitions.FindItem(gift.ItemId);
                if (item == null || string.IsNullOrWhiteSpace(gift.Effect)) continue;
                int index = _entries.Count;
                var entry = (gift, item);
                _entries.Add(entry);
                var choice = Instantiate(_choicePrefab, _choices);
                choice.gameObject.SetActive(true);
                choice.GetComponentInChildren<TMP_Text>().text = item.Name;
                choice.transform.Find("Icon").GetComponent<Image>().sprite = Icon(item);
                choice.onClick.AddListener(() => SelectGift(index));
                _buttons.Add(choice);
            }
            for (int i = 0; i < _targets.Length; i++)
            {
                int index = i;
                _targets[i].onClick.AddListener(() => SelectTarget(index));
            }
            _play.onClick.AddListener(Replay);
            _burst.onClick.AddListener(BurstFive);
            _clear.onClick.AddListener(Clear);
            _timeline.onValueChanged.AddListener(Scrub);
            _sound.onValueChanged.AddListener(SetSound);
            _loop.onValueChanged.AddListener(SetLoop);
            SelectGift(Mathf.Clamp(_selected, 0, Mathf.Max(0, _entries.Count - 1)));
            UpdateTarget();
        }

        private void OnDisable()
        {
            Clear();
            ReleaseSelection();
            foreach (var button in _buttons) { button.onClick.RemoveAllListeners(); Destroy(button.gameObject); }
            _buttons.Clear();
            foreach (var button in _targets) button.onClick.RemoveAllListeners();
            _play.onClick.RemoveListener(Replay);
            _burst.onClick.RemoveListener(BurstFive);
            _clear.onClick.RemoveListener(Clear);
            _timeline.onValueChanged.RemoveListener(Scrub);
            _sound.onValueChanged.RemoveListener(SetSound);
            _loop.onValueChanged.RemoveListener(SetLoop);
        }

        public void SelectGift(int index)
        {
            Clear();
            ReleaseSelection();
            _play.interactable = _burst.interactable = _timeline.interactable = false;
            if (_entries.Count == 0) { _title.text = "暂无礼物"; _play.interactable = _burst.interactable = false; return; }
            _selected = Mathf.Clamp(index, 0, _entries.Count - 1);
            _title.text = _entries[_selected].Item.Name;
            _selectionCancellation = new CancellationTokenSource();
            LoadSelection(_entries[_selected], _selectionCancellation.Token).Forget();
            for (int i = 0; i < _buttons.Count; i++)
                _buttons[i].GetComponent<Image>().color = i == _selected ? new Color32(131, 112, 66, 255) : new Color32(34, 61, 57, 255);
        }

        private void ReleaseSelection()
        {
            _selectionCancellation?.Cancel();
            _selectionCancellation?.Dispose();
            _selectionCancellation = null;
            _effect = null;
            _resources?.Dispose();
            _resources = null;
        }

        private async UniTaskVoid LoadSelection((GiftDefinition Gift, ItemDefinition Item) entry, CancellationToken cancellationToken)
        {
            RemoteAssets.Lease<GiftEffectSO> resources = null;
            try
            {
#if UNITY_EDITOR
                if (!_useRemoteResources)
                {
                    // Source definitions read the .lua.txt files on disk, so script edits apply on the next play.
                    _effect = UnityEditor.AssetDatabase.LoadAssetAtPath<GiftEffectSO>(RemoteAssets.SourcePath(entry.Gift.Effect));
                    _effect?.ReloadFromDisk();
                }
                else
#endif
                {
                    resources = await GiftEffectLoader.LoadAsync(entry.Gift.Effect, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    _effect = resources?.Asset;
                    _resources = resources;
                    resources = null;
                }
                if (_effect != null && !_effect.IsPlayable) _effect = null;
                _play.interactable = _burst.interactable = _effect != null;
                _timeline.interactable = _effect != null && _effect.SupportsTimeline;
                _status.text = _effect != null ? "就绪" : "预览暂不可用";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { resources?.Dispose(); }
        }

        // The Editor reads Configs/Items directly; a player build uses the catalog received from the server.
        private static GameCatalog Definitions()
        {
#if UNITY_EDITOR
            try { return CatalogConfiguration.Load(); }
            catch (Exception exception) { Debug.LogException(exception); return null; }
#else
            return ClientCatalog.Current;
#endif
        }

        private static Sprite Icon(ItemDefinition item)
        {
#if UNITY_EDITOR
            var icons = UnityEditor.AssetDatabase.LoadAssetAtPath<RemoteSpriteSetSO>(RemoteAssets.SourcePath(ItemArt.ItemIconsAddress));
            if (icons != null && icons.Find(item.Icon) != null) return icons.Find(item.Icon);
#endif
            return ShopLocalArtSO.ItemIcon(item);
        }

        public void SelectTarget(int index)
        {
            Clear();
            _target = Mathf.Clamp(index, 0, _targets.Length - 1);
            UpdateTarget();
            Replay();
        }

        private void UpdateTarget()
        {
            _targetLabel.text = "目标：" + _targets[_target].GetComponentInChildren<TMP_Text>().text;
            for (int i = 0; i < _targets.Length; i++)
                _targets[i].GetComponent<Image>().color = i == _target ? new Color32(93, 85, 55, 255) : new Color32(34, 61, 57, 255);
        }

        public void Replay()
        {
            if (_effect == null) return;
#if UNITY_EDITOR
            if (!_useRemoteResources && _active.Count == 0) _effect.ReloadFromDisk();
#endif
            ClearEffects();
            _queued = 1;
            _next = Time.unscaledTime;
        }

        public void BurstFive()
        {
            if (_effect == null) return;
            ClearEffects();
            _queued = 5;
            _next = Time.unscaledTime;
        }

        public void Clear()
        {
            ClearEffects();
            _loop.SetIsOnWithoutNotify(false);
        }

        private void ClearEffects()
        {
            foreach (var effect in _active) if (effect != null) { effect.Stop(); Destroy(effect.gameObject); }
            _active.Clear();
            _started.Clear();
            if (_pose != null) { _pose.Stop(); Destroy(_pose.gameObject); }
            _pose = null;
            _scrubbing = false;
            _queued = 0;
            _next = Time.unscaledTime + 0.7f;
            _timeline.SetValueWithoutNotify(0f);
            _status.text = "就绪";
        }

        private GiftEffect CreateEffect()
        {
            var effect = _effect.CreateEffect(_effectRoot);
            if (effect == null) return null;
            var rect = (RectTransform)effect.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            effect.SoundEnabled = _sound.isOn;
            return effect;
        }

        public void Scrub(float fraction)
        {
            if (_effect == null || !_effect.SupportsTimeline) return;
            if (!_scrubbing)
            {
                ClearEffects();
                _loop.SetIsOnWithoutNotify(false);
                _pose = CreateEffect();
                if (_pose == null) return;
                _scrubbing = true;
            }
            _pose.SoundEnabled = false;
            _pose.PreviewAt(_sender, (RectTransform)_targets[_target].transform, fraction * _pose.Duration);
            _timeline.SetValueWithoutNotify(fraction);
            _status.text = (fraction * _pose.Duration).ToString("0.00") + " / " + _pose.Duration.ToString("0.00") + " 秒";
        }

        private void SetSound(bool enabled)
        {
            foreach (var effect in _active) if (effect != null) effect.SoundEnabled = enabled;
        }

        private void SetLoop(bool enabled)
        {
            if (enabled && _active.Count == 0) Replay();
        }

        private void Update()
        {
            if (_effect == null || _scrubbing) return;
            float now = Time.unscaledTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i] != null && now - _started[i] < _active[i].Duration + 0.05f) continue;
                if (_active[i] != null) { _active[i].Stop(); Destroy(_active[i].gameObject); }
                _active.RemoveAt(i);
                _started.RemoveAt(i);
            }
            if (_queued == 0 && _active.Count == 0 && _loop.isOn && now >= _next) _queued = 1;
            if (_queued > 0 && now >= _next)
            {
                var effect = CreateEffect();
                if (effect == null) { _queued = 0; _status.text = "预览暂不可用"; return; }
                effect.Play(_sender, (RectTransform)_targets[_target].transform);
                _active.Add(effect);
                _started.Add(now);
                _lastStarted = now;
                _queued--;
                _next = now + (_queued > 0 ? GiftInteractionController.PlaybackInterval(effect.Duration) : effect.Duration + 0.7f);
            }
            if (_active.Count > 0)
            {
                float duration = _effect.Duration;
                float elapsed = Mathf.Min(now - _lastStarted, duration);
                _timeline.SetValueWithoutNotify(duration > 0f ? elapsed / duration : 0f);
                _status.text = elapsed.ToString("0.00") + " / " + duration.ToString("0.00") + " 秒";
            }
            else if (_queued == 0) _status.text = "就绪";
        }
    }
}
