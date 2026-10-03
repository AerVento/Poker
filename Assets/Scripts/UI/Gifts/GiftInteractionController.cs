using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Content;
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
        private readonly List<ItemConsumeCommandMessage> _pending = new List<ItemConsumeCommandMessage>();
        private readonly Dictionary<string, ItemConsumeCommandMessage[]> _inFlight =
            new Dictionary<string, ItemConsumeCommandMessage[]>(StringComparer.Ordinal);
        private readonly Queue<PlaybackUnit> _playback = new Queue<PlaybackUnit>();
        private readonly HashSet<string> _seenEvents = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _seenEventOrder = new Queue<string>();

        [SerializeField]
        private GiftMenuView _menuViewPrefab;

        private uint _dollars;
        private uint _availableChips;
        private bool _canExchange;
        private readonly Dictionary<string, GiftOffer> _offers = new Dictionary<string, GiftOffer>(StringComparer.Ordinal);
        private NetworkRoom _inventoryRoom;

        // A gift's shop price, and whether the server would let the player buy it right now.
        private readonly struct GiftOffer
        {
            public uint Price { get; }
            public bool CanPurchase { get; }
            public GiftOffer(uint price, bool canPurchase) { Price = price; CanPurchase = canPurchase; }
        }
        private readonly List<GiftDefinition> _gifts = new List<GiftDefinition>();
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
        private readonly List<PlaybackUnit> _pendingTargets = new List<PlaybackUnit>();
        private readonly Dictionary<string, OutgoingResource> _outgoingResources = new Dictionary<string, OutgoingResource>();
        private CancellationTokenSource _outgoingCancellation;

        private sealed class OutgoingResource
        {
            public bool Finished;
            public RemoteAssets.Lease<GiftEffectSO> Lease;
        }
        private CancellationTokenSource _playbackCancellation;
        private bool _playingQueue;
        private int _playbackVersion;
        private int _targetSeat = -1;
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
            ReloadGifts();
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

        // The gift menu lists Configs/Items/Gifts.json in order, as received from the server.
        private void ReloadGifts()
        {
            _gifts.Clear();
            foreach (var gift in ClientCatalog.Current?.Gifts ?? Array.Empty<GiftDefinition>())
                if (!string.IsNullOrWhiteSpace(gift.Effect) && ClientCatalog.FindItem(gift.ItemId) != null) _gifts.Add(gift);
            _selectedIndex = Mathf.Max(0, _gifts.FindIndex(g => g.ItemId == _selectedGiftId));
        }

        private void OnCatalogChanged()
        {
            ReloadGifts();
            RefreshMenu();
        }

        private void OnEnable()
        {
            Active = this;
            ClientCatalog.Changed += OnCatalogChanged;
            Game.Content.ItemArt.Changed += RefreshMenu;
            _menuBlockerButton.onClick.AddListener(CloseMenu);
            _giftButton.onClick.AddListener(QueueSelectedGift);
            _view.Previous.onClick.AddListener(PreviousGift);
            _view.Next.onClick.AddListener(NextGift);
            _inventoryRoom = Mirror.NetworkManager.singleton as NetworkRoom;
            if (_inventoryRoom != null) _inventoryRoom.CollectionReceived += ReceiveInventory;
            CloseMenu();
        }

        private void OnDisable()
        {
            ClientCatalog.Changed -= OnCatalogChanged;
            Game.Content.ItemArt.Changed -= RefreshMenu;
            _menuBlockerButton.onClick.RemoveListener(CloseMenu);
            _giftButton.onClick.RemoveListener(QueueSelectedGift);
            _view.Previous.onClick.RemoveListener(PreviousGift);
            _view.Next.onClick.RemoveListener(NextGift);
            if (_inventoryRoom != null) _inventoryRoom.CollectionReceived -= ReceiveInventory;
            _inventoryRoom = null;
            if (Active == this)
                Active = null;
            StopAllCoroutines();
            _captureRoutine = null;
            CancelPlayback();
            CancelOutgoing();
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
            PruneOutgoing();
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

        // The gift menu reads the shared inventory snapshot; giving is an item consume request with the Give method.
        private void ReceiveInventory(InventoryResponse response)
        {
            if (!isActiveAndEnabled) return;
            if (response.Success)
            {
                ApplyInventory(response.Snapshot);
                _feedback.text = string.Empty;
            }
            else if (_owned.Count == 0)
                _feedback.text = "礼物服务暂时不可用";
        }

        public void ReceiveGiveResponse(ItemActionResponse response)
        {
            if (!string.IsNullOrEmpty(response.OperationId))
                _inFlight.Remove(response.OperationId);
            if (response.HasSnapshot)
                ApplyInventory(response.Snapshot);
            _feedback.text = response.Result == ItemActionResult.Success
                ? "礼物已送出"
                : GiftResultText(response.Result);
            RefreshMenu();
        }

        public void ReceivePlayback(GiftPlaybackBatchMessage message)
        {
            if (!isActiveAndEnabled || _panel?.Info.RoomInfo == null) return;
            if (string.IsNullOrEmpty(message.EventId) || message.Commands == null || !_seenEvents.Add(message.EventId))
                return;
            _seenEventOrder.Enqueue(message.EventId);
            while (_seenEventOrder.Count > 256)
                _seenEvents.Remove(_seenEventOrder.Dequeue());
            foreach (var command in message.Commands)
            {
                var quantity = Math.Min(command.Quantity, GiftBatchLimits.MaxEffects);
                for (uint index = 0; index < quantity; index++)
                    _playback.Enqueue(new PlaybackUnit(command.ItemId, command.SenderSeatIndex, command.TargetSeatIndex,
                        _panel.Info.RoomInfo.Id,
                        _panel.Info.RoomInfo.FindSeat(command.SenderSeatIndex)?.PlayerName,
                        _panel.Info.RoomInfo.FindSeat(command.TargetSeatIndex)?.PlayerName));
            }
            if (!_playingQueue)
            {
                _playbackCancellation?.Dispose();
                _playbackCancellation = new CancellationTokenSource();
                _playingQueue = true;
                PlayQueue(_playbackVersion, _playbackCancellation.Token).Forget();
            }
        }

        public void ResetSession()
        {
            StopAllCoroutines();
            _captureRoutine = null;
            CancelPlayback();
            CancelOutgoing();
            _owned.Clear();
            _offers.Clear();
            _hasInventory = false;
            _dollars = 0;
            _availableChips = 0;
            _canExchange = false;
            _pending.Clear();
            _inFlight.Clear();
            _playback.Clear();
            _seenEvents.Clear();
            _seenEventOrder.Clear();
            foreach (var effect in _activeEffects.Where(effect => effect != null).ToArray())
                Destroy(effect.gameObject);
            _activeEffects.Clear();
            CloseMenu();
            RefreshMenu();
        }

        private void RequestInventory()
        {
            if (Mirror.NetworkManager.singleton is NetworkRoom room)
            {
                if (!_hasInventory) _feedback.text = "正在读取礼物库存…";
                room.LoadCollection(force: true);
            }
        }

        private void QueueSelectedGift()
        {
            if (_gifts.Count > 0) QueueGift(_gifts[_selectedIndex].ItemId, _targetSeat);
        }

        private void PreviousGift() => SelectGift(_selectedIndex - 1);
        private void NextGift() => SelectGift(_selectedIndex + 1);

        private void SelectGift(int index)
        {
            if (index < 0 || index >= _gifts.Count) return;
            _selectedIndex = index;
            _selectedGiftId = _gifts[index].ItemId;
            _feedback.text = string.Empty;
            RefreshMenu();
        }

        public bool QueueGift(string itemId, int targetSeatIndex)
        {
            if (!isActiveAndEnabled) return false;
            PruneOutgoing();
            if (string.IsNullOrWhiteSpace(ClientCatalog.FindGift(itemId)?.Effect))
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
            var roomInfo = _panel.Info.RoomInfo;
            var sender = roomInfo.FindSeat(self);
            var target = roomInfo.FindSeat(targetSeatIndex);
            if (sender == null || sender.State == RoomSeatState.Leaving || target.State == RoomSeatState.Leaving)
                return false;
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
                _pending.Add(new ItemConsumeCommandMessage
                {
                    ItemId = itemId,
                    TargetSeatIndex = targetSeatIndex,
                    Quantity = 1,
                });
                _pendingTargets.Add(new PlaybackUnit(itemId, sender.SeatIndex, targetSeatIndex,
                    roomInfo.Id, self, target.PlayerName));
            }
            if (!_outgoingResources.ContainsKey(itemId))
            {
                var resource = new OutgoingResource();
                _outgoingResources.Add(itemId, resource);
                if (_outgoingCancellation == null) _outgoingCancellation = new CancellationTokenSource();
                PrepareOutgoing(itemId, resource, _outgoingCancellation.Token).Forget();
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
            while (true)
            {
                PruneOutgoing();
                if (_pending.Count == 0) break;
                if (_inFlight.Count == 0 && _pending.All(command =>
                    _outgoingResources.TryGetValue(command.ItemId, out var resource) &&
                    resource.Finished && resource.Lease != null)) break;
                yield return null;
            }
            _captureRoutine = null;
            if (_pending.Count == 0)
                yield break;
            var commands = _pending.ToArray();
            _pending.Clear();
            _pendingTargets.Clear();
            var room = Mirror.NetworkManager.singleton as NetworkRoom;
            var operationId = Guid.NewGuid().ToString("N");
            var requestId = room?.RequestItemConsume(new ItemConsumeRequest
            {
                OperationId = operationId,
                Method = ItemConsumeMethod.Give,
                CatalogRevision = ClientCatalog.Revision,
                Commands = commands,
            }) ?? 0;
            if (requestId == 0)
            {
                _feedback.text = "当前无法发送礼物";
                RefreshMenu();
                yield break;
            }
            _inFlight[operationId] = commands;
            _feedback.text = "正在发送礼物…";
            RefreshMenu();
        }

        private async UniTaskVoid PrepareOutgoing(string itemId, OutgoingResource resource, CancellationToken token)
        {
            RemoteAssets.Lease<GiftEffectSO> lease = null;
            try
            {
                lease = await GiftEffectLoader.LoadAsync(ClientCatalog.FindGift(itemId)?.Effect, token);
                token.ThrowIfCancellationRequested();
                resource.Lease = lease;
                lease = null;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                lease?.Dispose();
                resource.Finished = true;
            }
        }

        private void PruneOutgoing()
        {
            if (_pending.Count == 0 && _outgoingResources.Count == 0) return;
            var changed = false;
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var command = _pending[i];
                var failed = _outgoingResources.TryGetValue(command.ItemId, out var resource) &&
                    resource.Finished && resource.Lease == null;
                if (!failed && !IsRestricted && IsCurrent(_pendingTargets[i])) continue;
                _pending.RemoveAt(i);
                _pendingTargets.RemoveAt(i);
                _feedback.text = failed ? "下载失败，请重试" : "待发送礼物已取消";
                changed = true;
            }
            foreach (var item in _outgoingResources.Where(pair => pair.Value.Finished &&
                !_pending.Any(command => command.ItemId == pair.Key) &&
                !_inFlight.Values.Any(batch => batch.Any(command => command.ItemId == pair.Key))).ToArray())
            {
                item.Value.Lease?.Dispose();
                _outgoingResources.Remove(item.Key);
            }
            if (changed) RefreshMenu();
        }

        private void CancelOutgoing()
        {
            _outgoingCancellation?.Cancel();
            _outgoingCancellation?.Dispose();
            _outgoingCancellation = null;
            foreach (var resource in _outgoingResources.Values) resource.Lease?.Dispose();
            _outgoingResources.Clear();
            _pendingTargets.Clear();
        }

        private void CancelPlayback()
        {
            _playbackVersion++;
            _playbackCancellation?.Cancel();
            _playbackCancellation?.Dispose();
            _playbackCancellation = null;
            _playingQueue = false;
        }

        private async UniTaskVoid PlayQueue(int version, CancellationToken cancellationToken)
        {
            // A failed gift must not retry once for every unit in a burst.
            var failed = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                while (_playback.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var unit = _playback.Dequeue();
                    var entry = ClientCatalog.FindGift(unit.ItemId);
                    if (string.IsNullOrWhiteSpace(entry?.Effect) || failed.Contains(unit.ItemId) || !IsCurrent(unit)) continue;
                    var lease = await GiftEffectLoader.LoadAsync(entry.Effect, cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (lease == null)
                        {
                            failed.Add(unit.ItemId);
                            continue;
                        }
                        if (!IsCurrent(unit)) continue;
                        var duration = PlayOne(unit, lease);
                        if (duration <= 0f) continue;
                        lease = null; // The instance now owns the resource until OnDestroy.
                        await UniTask.Delay(TimeSpan.FromSeconds(PlaybackInterval(duration)),
                            ignoreTimeScale: true, cancellationToken: cancellationToken);
                    }
                    finally { lease?.Dispose(); }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                if (version == _playbackVersion) _playingQueue = false;
            }
        }

        private bool IsCurrent(PlaybackUnit unit)
        {
            var room = _panel?.Info.RoomInfo;
            if (!isActiveAndEnabled || room == null || room.Id != unit.RoomId || room.IsWaiting) return false;
            var target = room.FindSeat(unit.TargetSeatIndex);
            var sender = room.FindSeat(unit.SenderSeatIndex);
            return !string.IsNullOrEmpty(unit.TargetName) && !string.IsNullOrEmpty(unit.SenderName) &&
                target?.PlayerName == unit.TargetName && sender?.PlayerName == unit.SenderName &&
                target.State != RoomSeatState.Leaving && sender.State != RoomSeatState.Leaving;
        }

        private float PlayOne(PlaybackUnit unit, RemoteAssets.Lease<GiftEffectSO> resources)
        {
            var target = ResolveSeat(unit.TargetSeatIndex);
            var sender = ResolveSeat(unit.SenderSeatIndex);
            if (target == null || sender == null) return 0f;
            var effect = resources.Asset.CreateEffect(_effectRoot);
            if (effect == null) return 0f;
            effect.RetainResources(resources);
            var rect = (RectTransform)effect.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _activeEffects.Add(effect);
            effect.Play(sender, target);
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
                if (!CurrencySystem.CanAfford(cost, _dollars, _availableChips, _canExchange)) return false;
            }
            return true;
        }

        private void ApplyInventory(InventorySnapshotMessage snapshot)
        {
            var catalog = ClientCatalog.Current;
            _hasInventory = true;
            _dollars = snapshot.Dollars;
            _availableChips = snapshot.AvailableChips;
            _canExchange = snapshot.CanExchange;
            _owned.Clear();
            foreach (var item in snapshot.Items ?? Array.Empty<ItemStackMessage>())
                if (catalog?.FindGift(item.ItemId) != null) _owned[item.ItemId] = item.Quantity;
            _offers.Clear();
            foreach (var state in snapshot.Offers ?? Array.Empty<ItemOfferStateMessage>())
            {
                var offer = catalog?.FindOffer(state.ItemId);
                if (offer != null && catalog.FindGift(state.ItemId) != null)
                    _offers[state.ItemId] = new GiftOffer(offer.Price, state.CanPurchase);
            }
            // Players serving a sentence cannot give gifts.
            _canSend = catalog == null || !catalog.IsServing(catalog.ServiceYears(
                (snapshot.Items ?? Array.Empty<ItemStackMessage>()).Select(i => new ItemStack(i.ItemId, i.Quantity))));
            RefreshMenu();
        }

        private bool IsRestricted => (_hasInventory && !_canSend) ||
            ClientCatalog.Current?.IsServing(_panel?.Info.RoomInfo?.GetServiceYears(
                GameController.Available ? GameController.Instance.State.UserName : null) ?? 0UL) == true;

        private void RefreshMenu()
        {
            if (_quantity == null)
                return;
            var gift = _gifts.Count > 0 ? _gifts[_selectedIndex] : null;
            var item = gift != null ? ClientCatalog.FindItem(gift.ItemId) : null;
            var quantity = gift != null ? Available(gift.ItemId) : 0;
            var affordable = gift != null && CanAffordGift(gift.ItemId);
            var offer = default(GiftOffer);
            var hasOffer = gift != null && _offers.TryGetValue(gift.ItemId, out offer);
            _quantity.text = gift == null ? "暂无礼物" : item?.Name ?? gift.ItemId;
            _view.UseCost.text = gift == null ? string.Empty : !_hasInventory ? "加载中…" : quantity > 0
                ? $"× {quantity:N0}" : hasOffer ? $"${offer.Price:N0}" : "暂不可用";
            _view.UseCost.color = quantity > 0 || affordable ? new Color32(238, 233, 221, 255) : new Color32(235, 91, 91, 255);
            _view.DownloadView.Bind(gift?.Effect, false);
            _view.GiftIcon.sprite = item != null ? ShopLocalArtSO.ItemIcon(item) : null;
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

        private static string GiftResultText(ItemActionResult result)
        {
            return result switch
            {
                ItemActionResult.RateLimited => "操作太快，请稍后再试",
                ItemActionResult.RoomStartedRequired => "礼物只能在牌局开始后使用",
                ItemActionResult.SenderNotSeated => "只有牌桌玩家可以送礼",
                ItemActionResult.TargetNotFound => "目标玩家已经离开",
                ItemActionResult.CannotTargetSelf => "不能给自己送礼",
                ItemActionResult.NotOwned => "尚未拥有这个礼物",
                ItemActionResult.InsufficientItems => "礼物库存不足",
                ItemActionResult.ServiceRestricted => "服刑期间无法送礼",
                ItemActionResult.InsufficientDollars => "余额不足",
                ItemActionResult.UnknownItem => "客户端需要更新礼物资源",
                ItemActionResult.CatalogChanged => "礼物配置已更新，请重新选择",
                ItemActionResult.NotConsumable => "这个物品当前不能作为礼物使用",
                ItemActionResult.MethodUnavailable => "这个物品当前不能作为礼物使用",
                ItemActionResult.InvalidRequest => "礼物请求无效",
                _ => "礼物服务暂时不可用",
            };
        }

        private readonly struct PlaybackUnit
        {
            public string ItemId { get; }
            public int SenderSeatIndex { get; }
            public int TargetSeatIndex { get; }
            public int RoomId { get; }
            public string SenderName { get; }
            public string TargetName { get; }

            public PlaybackUnit(string itemId, int senderSeatIndex, int targetSeatIndex,
                int roomId, string senderName, string targetName)
            {
                ItemId = itemId;
                SenderSeatIndex = senderSeatIndex;
                TargetSeatIndex = targetSeatIndex;
                RoomId = roomId;
                SenderName = senderName;
                TargetName = targetName;
            }
        }
    }

}
