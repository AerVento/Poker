using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Network;
using Game.SO;
using Game.Network.Accounts;
using Game.UI.GamePanelElement;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI.Gifts
{
    [DisallowMultipleComponent]
    public sealed class GiftInteractionController : MonoBehaviour
    {
        private const float CaptureWindowSeconds = 0.2f;
        private const float MaximumSimultaneousEffects = 10f;
        private readonly Dictionary<string, uint> _owned = new Dictionary<string, uint>(StringComparer.Ordinal);
        private readonly List<GiftUseCommandMessage> _pending = new List<GiftUseCommandMessage>();
        private readonly Dictionary<string, GiftUseCommandMessage[]> _inFlight =
            new Dictionary<string, GiftUseCommandMessage[]>(StringComparer.Ordinal);
        private readonly Queue<PlaybackUnit> _playback = new Queue<PlaybackUnit>();
        private readonly HashSet<string> _seenEvents = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _seenEventOrder = new Queue<string>();

        [SerializeField]
        private GiftMenuView _menuViewPrefab;

        private uint _dollars;
        private readonly Dictionary<string, GiftOfferMessage> _offers = new Dictionary<string, GiftOfferMessage>(StringComparer.Ordinal);
        private readonly List<GiftCatalogSO.Entry> _gifts = new List<GiftCatalogSO.Entry>();
        private static string _selectedGiftId;
        private int _selectedIndex;
        private GiftMenuView _view;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSelection() => _selectedGiftId = null;

        private bool _hasInventory;
        private bool _canSend;
        private GameObject _restrictedOverlay;
        private GamePanel _panel;
        private RectTransform _overlay;
        private RectTransform _menuBlocker;
        private RectTransform _effectRoot;
        private RectTransform _menu;
        private TMP_Text _menuTitle;
        private TMP_Text _quantity;
        private TMP_Text _feedback;
        private Button _giftButton;
        private Button _menuBlockerButton;
        private GameObject _previousSelection;
        private Coroutine _captureRoutine;
        private Coroutine _playbackRoutine;
        private int _targetSeat = -1;
        private ulong _lastInventoryRequest;
        private readonly List<GiftEffect> _activeEffects = new List<GiftEffect>();

        public static GiftInteractionController Active { get; private set; }

        public static float PlaybackInterval(float fullDuration)
            => Mathf.Max(0f, fullDuration) / MaximumSimultaneousEffects;

        public static GiftInteractionController EnsureActive()
        {
            if (Active != null && Active.isActiveAndEnabled)
                return Active;
            foreach (var panel in FindObjectsOfType<GamePanel>(true))
            {
                if (!panel.isActiveAndEnabled)
                    continue;
                return panel.GetComponent<GiftInteractionController>();
            }
            return null;
        }

        private void Awake()
        {
            _panel = GetComponent<GamePanel>();
            var view = Instantiate(_menuViewPrefab, transform, false);
            _view = view;
            if (GiftCatalogSO.Instance != null)
                _gifts.AddRange(GiftCatalogSO.Instance.Gifts.Where(g => g != null && g.Item != null && g.EffectPrefab != null));
            _selectedIndex = Mathf.Max(0, _gifts.FindIndex(g => g.Id == _selectedGiftId));
            _overlay = (RectTransform)view.transform;
            _restrictedOverlay = view.RestrictedOverlay;
            _menuBlocker = view.MenuBlocker;
            _effectRoot = view.EffectRoot;
            _menu = view.Menu;
            _menuTitle = view.MenuTitle;
            _quantity = view.Quantity;
            _feedback = view.Feedback;
            _giftButton = view.GiftButton;
            _menuBlockerButton = view.MenuBlockerButton;
        }

        private void OnEnable()
        {
            Active = this;
            _menuBlockerButton.onClick.AddListener(CloseMenu);
            _giftButton.onClick.AddListener(QueueSelectedGift);
            _view.Previous.onClick.AddListener(PreviousGift);
            _view.Next.onClick.AddListener(NextGift);
            CloseMenu();
        }

        private void OnDisable()
        {
            _menuBlockerButton.onClick.RemoveListener(CloseMenu);
            _giftButton.onClick.RemoveListener(QueueSelectedGift);
            _view.Previous.onClick.RemoveListener(PreviousGift);
            _view.Next.onClick.RemoveListener(NextGift);
            if (Active == this)
                Active = null;
            StopAllCoroutines();
            _captureRoutine = null;
            _playbackRoutine = null;
            _pending.Clear();
            _inFlight.Clear();
            _playback.Clear();
            foreach (var effect in _activeEffects.Where(effect => effect != null).ToArray())
                Destroy(effect.gameObject);
            _activeEffects.Clear();
            CloseMenu();
        }

        private void Update()
        {
            if (_menu != null && _menu.gameObject.activeSelf && _restrictedOverlay != null &&
                _restrictedOverlay.activeSelf != IsRestricted)
                RefreshMenu();
            if (_menu != null && _menu.gameObject.activeSelf && Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
                CloseMenu();
        }

        internal void OpenFor(PlayerListItem item)
        {
            if (item == null || _panel?.Info.RoomInfo == null || _panel.Info.RoomInfo.IsWaiting)
                return;
            var seat = _panel.Info.RoomInfo.FindSeat(item.PlayerName);
            var self = GameController.Available ? GameController.Instance.State.UserName : null;
            if (seat == null || seat.PlayerName == self || seat.State == RoomSeatState.Leaving)
                return;
            _targetSeat = seat.SeatIndex;
            if (!_menu.gameObject.activeSelf && EventSystem.current != null)
                _previousSelection = EventSystem.current.currentSelectedGameObject;
            _menuTitle.text = $"送给 {seat.PlayerName}";
            PositionMenu((RectTransform)item.transform);
            _menuBlocker.gameObject.SetActive(true);
            _menu.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_giftButton.gameObject);
            RequestInventory();
            RefreshMenu();
        }

        public void ReceiveInventory(GiftInventoryResponse response)
        {
            if (response.RequestId < _lastInventoryRequest)
                return;
            _lastInventoryRequest = response.RequestId;
            if (response.Result == GiftUseResult.Success)
            {
                ApplyInventory(response.Items, response.Dollars, response.Offers, response.CanSend);
                _feedback.text = string.Empty;
            }
            else if (_owned.Count == 0)
                _feedback.text = GiftResultText(response.Result);
        }

        public void ReceiveUseResponse(GiftUseBatchResponse response)
        {
            if (!string.IsNullOrEmpty(response.OperationId))
                _inFlight.Remove(response.OperationId);
            if (response.HasSnapshot && response.RequestId >= _lastInventoryRequest)
            {
                _lastInventoryRequest = response.RequestId;
                ApplyInventory(response.Items, response.Dollars, response.Offers, response.CanSend);
            }
            _feedback.text = response.Result == GiftUseResult.Success
                ? "礼物已送出"
                : GiftResultText(response.Result);
            RefreshMenu();
        }

        public void ReceivePlayback(GiftPlaybackBatchMessage message)
        {
            if (string.IsNullOrEmpty(message.EventId) || message.Commands == null || !_seenEvents.Add(message.EventId))
                return;
            _seenEventOrder.Enqueue(message.EventId);
            while (_seenEventOrder.Count > 256)
                _seenEvents.Remove(_seenEventOrder.Dequeue());
            foreach (var command in message.Commands)
            {
                var quantity = Math.Min(command.Quantity, GiftBatchLimits.MaxEffects);
                for (uint index = 0; index < quantity; index++)
                    _playback.Enqueue(new PlaybackUnit(command.ItemId, command.TargetSeatIndex));
            }
            if (_playbackRoutine == null)
                _playbackRoutine = StartCoroutine(PlayQueue());
        }

        public void ResetSession()
        {
            StopAllCoroutines();
            _captureRoutine = null;
            _playbackRoutine = null;
            _owned.Clear();
            _offers.Clear();
            _hasInventory = false;
            _dollars = 0;
            _pending.Clear();
            _inFlight.Clear();
            _playback.Clear();
            _seenEvents.Clear();
            _seenEventOrder.Clear();
            foreach (var effect in _activeEffects.Where(effect => effect != null).ToArray())
                Destroy(effect.gameObject);
            _activeEffects.Clear();
            _lastInventoryRequest = 0;
            CloseMenu();
            RefreshMenu();
        }

        private void RequestInventory()
        {
            if (Mirror.NetworkManager.singleton is NetworkRoom room)
            {
                var request = room.RequestGiftInventory();
                if (request > _lastInventoryRequest)
                    _feedback.text = "正在读取礼物库存…";
            }
        }

        private void QueueSelectedGift()
        {
            if (_gifts.Count > 0) QueueGift(_gifts[_selectedIndex].Id, _targetSeat);
        }

        private void PreviousGift() => SelectGift(_selectedIndex - 1);
        private void NextGift() => SelectGift(_selectedIndex + 1);

        private void SelectGift(int index)
        {
            if (index < 0 || index >= _gifts.Count) return;
            _selectedIndex = index;
            _selectedGiftId = _gifts[index].Id;
            _feedback.text = string.Empty;
            RefreshMenu();
        }

        public bool QueueGift(string itemId, int targetSeatIndex)
        {
            if (GiftCatalogSO.Instance?.Find(itemId)?.EffectPrefab == null)
            {
                _feedback.text = "当前礼物暂不支持播放";
                return false;
            }
            if (targetSeatIndex < 0 || _panel?.Info.RoomInfo == null ||
                _panel.Info.RoomInfo.IsWaiting || _panel.Info.RoomInfo.FindSeat(targetSeatIndex) == null)
            {
                _feedback.text = "请选择牌局中的其他玩家";
                return false;
            }
            if (IsRestricted || !_hasInventory || (Available(itemId) == 0 && !CanAffordGift(itemId)))
                return false;
            if (_pending.Aggregate(0UL, (total, command) => total + command.Quantity) >= GiftBatchLimits.MaxEffects)
            {
                _feedback.text = "单批礼物数量已达到上限";
                return false;
            }
            var self = GameController.Available ? GameController.Instance.State.UserName : null;
            if (_panel.Info.RoomInfo.FindSeat(targetSeatIndex).PlayerName == self)
            {
                _feedback.text = "不能给自己发送礼物";
                return false;
            }
            var index = _pending.Count - 1;
            if (index >= 0 && _pending[index].ItemId == itemId &&
                _pending[index].TargetSeatIndex == targetSeatIndex)
            {
                var command = _pending[index];
                command.Quantity++;
                _pending[index] = command;
            }
            else
            {
                if (_pending.Count >= GiftBatchLimits.MaxCommands)
                {
                    _feedback.text = "这一批礼物种类过多";
                    return false;
                }
                _pending.Add(new GiftUseCommandMessage
                {
                    ItemId = itemId,
                    TargetSeatIndex = targetSeatIndex,
                    Quantity = 1,
                });
            }
            _feedback.text = "";
            RefreshMenu();
            if (_captureRoutine == null)
                _captureRoutine = StartCoroutine(FlushAfterCaptureWindow());
            return true;
        }

        private IEnumerator FlushAfterCaptureWindow()
        {
            yield return new WaitForSecondsRealtime(CaptureWindowSeconds);
            while (_inFlight.Count > 0)
                yield return null;
            _captureRoutine = null;
            if (_pending.Count == 0)
                yield break;
            var commands = _pending.ToArray();
            _pending.Clear();
            var room = Mirror.NetworkManager.singleton as NetworkRoom;
            var operationId = room?.RequestGiftUseBatch(commands);
            if (string.IsNullOrEmpty(operationId))
            {
                _feedback.text = "当前无法发送礼物";
                RefreshMenu();
                yield break;
            }
            _inFlight[operationId] = commands;
            _feedback.text = "正在发送礼物…";
            RefreshMenu();
        }

        private IEnumerator PlayQueue()
        {
            try
            {
                while (_playback.Count > 0)
                {
                    var unit = _playback.Dequeue();
                    var duration = PlayOne(unit);
                    yield return duration > 0f ? new WaitForSecondsRealtime(PlaybackInterval(duration)) : null;
                }
            }
            finally
            {
                _playbackRoutine = null;
            }
        }

        private float PlayOne(PlaybackUnit unit)
        {
            var prefab = GiftCatalogSO.Instance?.Find(unit.ItemId)?.EffectPrefab;
            if (prefab == null)
                return 0f;
            var target = ResolveSeat(unit.TargetSeatIndex);
            if (target == null)
                return 0f;
            var effect = Instantiate(prefab, _effectRoot, false);
            var rect = (RectTransform)effect.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _activeEffects.Add(effect);
            effect.Play(target);
            StartCoroutine(DestroyAfter(effect, effect.Duration + 0.05f));
            return effect.Duration;
        }

        private IEnumerator DestroyAfter(GiftEffect effect, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _activeEffects.Remove(effect);
            if (effect != null)
                Destroy(effect.gameObject);
        }

        private RectTransform ResolveSeat(int seatIndex)
        {
            foreach (var item in GetComponentsInChildren<PlayerListItem>(true))
            {
                if (!item.gameObject.activeInHierarchy)
                    continue;
                var seat = _panel.Info.RoomInfo.FindSeat(item.PlayerName);
                if (seat?.SeatIndex == seatIndex)
                    return (RectTransform)item.transform;
            }
            return null;
        }

        private uint Available(string itemId)
        {
            var owned = _owned.TryGetValue(itemId, out var value) ? value : 0;
            ulong reserved = (ulong)_pending.Where(command => command.ItemId == itemId).Sum(command => (long)command.Quantity);
            foreach (var batch in _inFlight.Values)
                reserved += (ulong)batch.Where(command => command.ItemId == itemId).Sum(command => (long)command.Quantity);
            return reserved >= owned ? 0 : owned - (uint)reserved;
        }

        private bool CanAffordGift(string itemId)
        {
            if (!_hasInventory || !_offers.TryGetValue(itemId, out var offer) || !offer.CanPurchase)
                return false;
            var reserved = new Dictionary<string, ulong>(StringComparer.Ordinal);
            foreach (var command in _pending.Concat(_inFlight.Values.SelectMany(batch => batch)))
            {
                reserved.TryGetValue(command.ItemId, out var count);
                reserved[command.ItemId] = count + command.Quantity;
            }
            reserved.TryGetValue(itemId, out var selectedCount);
            reserved[itemId] = selectedCount + 1;
            ulong cost = 0;
            foreach (var entry in reserved)
            {
                _owned.TryGetValue(entry.Key, out var owned);
                if (entry.Value <= owned) continue;
                if (!_offers.TryGetValue(entry.Key, out var quote) || !quote.CanPurchase) return false;
                cost += (entry.Value - owned) * quote.Price;
                if (cost > _dollars) return false;
            }
            return true;
        }

        private void ApplyInventory(ItemStackMessage[] items, uint dollars, GiftOfferMessage[] offers, bool canSend)
        {
            _hasInventory = true;
            _canSend = canSend;
            _dollars = dollars;
            _offers.Clear();
            foreach (var offer in offers ?? Array.Empty<GiftOfferMessage>())
                if (!string.IsNullOrEmpty(offer.ItemId)) _offers[offer.ItemId] = offer;
            _owned.Clear();
            foreach (var item in items ?? Array.Empty<ItemStackMessage>())
                _owned[item.ItemId] = item.Quantity;
            RefreshMenu();
        }

        private bool IsRestricted => (_hasInventory && !_canSend) ||
            InfernalNamecard.IsForced(_panel?.Info.RoomInfo?.GetServiceYears(
                GameController.Available ? GameController.Instance.State.UserName : null) ?? 0UL);

        private void RefreshMenu()
        {
            if (_quantity == null)
                return;
            var gift = _gifts.Count > 0 ? _gifts[_selectedIndex] : null;
            var quantity = gift != null ? Available(gift.Id) : 0;
            var affordable = gift != null && CanAffordGift(gift.Id);
            var offer = default(GiftOfferMessage);
            var hasOffer = gift != null && _offers.TryGetValue(gift.Id, out offer);
            _quantity.text = gift == null ? "暂无礼物" : gift.Item.DisplayName;
            _view.UseCost.text = gift == null ? string.Empty : !_hasInventory ? "加载中…" : quantity > 0
                ? $"× {quantity:N0}" : hasOffer ? $"${offer.Price:N0}" : "暂不可用";
            _view.UseCost.color = quantity > 0 || affordable ? new Color32(238, 233, 221, 255) : new Color32(235, 91, 91, 255);
            _view.GiftIcon.sprite = gift?.Item.Icon;
            _view.GiftIcon.enabled = _view.GiftIcon.sprite != null;
            _view.Page.text = _gifts.Count == 0 ? "0 / 0" : $"{_selectedIndex + 1} / {_gifts.Count}";
            _view.Previous.interactable = _selectedIndex > 0;
            _view.Next.interactable = _selectedIndex + 1 < _gifts.Count;
            if (_restrictedOverlay != null)
                _restrictedOverlay.SetActive(IsRestricted);
            _giftButton.interactable = gift != null && !IsRestricted && _hasInventory &&
                (quantity > 0 || affordable) && _targetSeat >= 0;
        }

        private void CloseMenu()
        {
            _targetSeat = -1;
            var eventSystem = EventSystem.current;
            var selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
            var restoreSelection = selected != null && _overlay != null && selected.transform.IsChildOf(_overlay);
            if (_menuBlocker != null)
                _menuBlocker.gameObject.SetActive(false);
            if (_menu != null)
                _menu.gameObject.SetActive(false);
            if (eventSystem != null && restoreSelection)
                eventSystem.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy
                    ? _previousSelection : null);
            _previousSelection = null;
        }

        private void PositionMenu(RectTransform target)
        {
            var canvas = target.GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var screen = RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(target.rect.center));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlay, screen, camera, out var point);
            var bounds = _overlay.rect;
            var half = _menu.sizeDelta * 0.5f;
            point.x += target.rect.width * 0.5f + half.x + 12f;
            point.x = Mathf.Clamp(point.x, bounds.xMin + half.x + 8f, bounds.xMax - half.x - 8f);
            point.y = Mathf.Clamp(point.y, bounds.yMin + half.y + 8f, bounds.yMax - half.y - 8f);
            _menu.anchoredPosition = point;
        }

        private static string GiftResultText(GiftUseResult result)
        {
            return result switch
            {
                GiftUseResult.RateLimited => "操作太快，请稍后再试",
                GiftUseResult.RoomNotStarted => "礼物只能在牌局开始后使用",
                GiftUseResult.SenderNotSeated => "只有牌桌玩家可以送礼",
                GiftUseResult.TargetNotFound => "目标玩家已经离开",
                GiftUseResult.CannotTargetSelf => "不能给自己送礼",
                GiftUseResult.NotOwned => "尚未拥有这个礼物",
                GiftUseResult.InsufficientItems => "礼物库存不足",
                GiftUseResult.ServiceRestricted => "服刑期间无法送礼",
                GiftUseResult.InsufficientDollars => "美金不足",
                GiftUseResult.UnknownGift => "客户端需要更新礼物资源",
                GiftUseResult.UseNotAllowed => "这个物品当前不能作为礼物使用",
                GiftUseResult.InvalidRequest => "礼物请求无效",
                _ => "礼物服务暂时不可用",
            };
        }

        private readonly struct PlaybackUnit
        {
            public string ItemId { get; }
            public int TargetSeatIndex { get; }

            public PlaybackUnit(string itemId, int targetSeatIndex)
            {
                ItemId = itemId;
                TargetSeatIndex = targetSeatIndex;
            }
        }
    }

}
