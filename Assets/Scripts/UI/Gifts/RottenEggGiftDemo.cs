using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.UI.Gifts
{
    /// <summary>Self-contained art playground. Does not connect to a server or spend items.</summary>
    public sealed class RottenEggGiftDemo : MonoBehaviour
    {
        [SerializeField] private RottenEggGiftEffect _effectPrefab;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private bool _autoLoop = true;
        private RectTransform _canvas;
        private RectTransform _menu;
        private RectTransform _menuBackdrop;
        private RectTransform _selected;
        private RottenEggGiftEffect _effect;
        private TMP_Text _status;
        private TMP_Text _loopLabel;
        private TMP_Text _recipient;
        private RectTransform _progress;
        private float _nextPlay;
        private readonly RectTransform[] _cards = new RectTransform[3];
        private readonly string[] _names = { "小桃", "阿凯", "老王" };
        private int _selectedIndex = 1;
        private static readonly Color Cream = new Color32(245, 240, 218, 255);
        private static readonly Color Mint = new Color32(203, 230, 144, 255);
        public RottenEggGiftEffect Effect => _effect;
        public RectTransform SelectedCard => _selected;

        private void Start()
        {
            Build();
            _nextPlay = Time.unscaledTime + 0.7f;
        }

        private void Update()
        {
            if (_effect == null) return;
            _status.text = _effect.Phase;
            _progress.sizeDelta = new Vector2(660f * (_effect.IsPlaying ? Mathf.Clamp01(_effect.Elapsed / _effect.Duration) : 0f), 3f);
            if (_effect.IsPlaying) _nextPlay = Time.unscaledTime + 1.1f;
            if (_autoLoop && !_menuBackdrop.gameObject.activeSelf && !_effect.IsPlaying && Time.unscaledTime >= _nextPlay)
                Replay();
        }

        public void Replay()
        {
            CloseMenu();
            _effect.Play(_selected);
            _nextPlay = Time.unscaledTime + _effect.Duration + 1.1f;
        }

        public void SelectCard(int index)
        {
            _selectedIndex = Mathf.Clamp(index, 0, _cards.Length - 1);
            _selected = _cards[_selectedIndex];
            _recipient.text = "送给 " + _names[_selectedIndex];
            _menuBackdrop.gameObject.SetActive(true);
            _menu.anchoredPosition = _selected.anchoredPosition + new Vector2(0, -142f);
        }

        public void CloseMenu()
        {
            _menuBackdrop.gameObject.SetActive(false);
            _nextPlay = Time.unscaledTime + 1.1f;
        }

        private void ToggleLoop()
        {
            _autoLoop = !_autoLoop;
            _loopLabel.text = _autoLoop ? "循环播放：开" : "循环播放：关";
            _nextPlay = Time.unscaledTime + 0.4f;
        }

        private void Build()
        {
            _canvas = RottenEggGiftEffect.Node("Gift preview canvas", transform, Vector2.zero);
            var canvas = _canvas.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _canvas.gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                var events = new GameObject("Gift preview input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            var bg = Panel("Background", _canvas, Vector2.zero, Vector2.zero, new Color32(12, 24, 29, 255));
            Stretch(bg);
            Label("POKER  /  INTERACTIVE GIFTS", _canvas, new Vector2(-470, 374), new Vector2(520, 28), 18, Mint, TextAlignmentOptions.Left);
            Label("臭鸡蛋", _canvas, new Vector2(-550, 318), new Vector2(360, 68), 52, Cream, TextAlignmentOptions.Left);
            Label("一点恶作剧，给牌桌加点戏。", _canvas, new Vector2(-425, 262), new Vector2(610, 38), 21, new Color32(139, 162, 158, 255), TextAlignmentOptions.Left);
            Panel("Top rule", _canvas, new Vector2(0, 225), new Vector2(1460, 1), new Color32(45, 64, 63, 255));
            Label("01  /  臭鸡蛋特效预览", _canvas, new Vector2(540, 368), new Vector2(390, 32), 20, Cream, TextAlignmentOptions.Right);
            Label("点击名片，选择礼物", _canvas, new Vector2(540, 330), new Vector2(390, 30), 18, new Color32(139, 162, 158, 255), TextAlignmentOptions.Right);
            var rim = RottenEggGiftEffect.Accent("Table rim", _canvas, new Vector2(1290, 420), RottenEggAccent.Shape.Disc, new Color32(41, 62, 59, 255));
            rim.anchoredPosition = new Vector2(0, -30);
            var table = RottenEggGiftEffect.Accent("Table felt", _canvas, new Vector2(1260, 392), RottenEggAccent.Shape.Disc, new Color32(22, 53, 48, 255));
            table.anchoredPosition = rim.anchoredPosition;
            var line = RottenEggGiftEffect.Accent("Table stitch", _canvas, new Vector2(1190, 332), RottenEggAccent.Shape.Ring, new Color32(49, 80, 64, 255));
            line.anchoredPosition = rim.anchoredPosition;
            Label("POKER", _canvas, new Vector2(0, -86), new Vector2(360, 50), 35, new Color32(69, 102, 80, 255));
            Label("GIFT PLAYGROUND", _canvas, new Vector2(0, -123), new Vector2(320, 26), 13, new Color32(69, 102, 80, 255));
            Vector2[] positions = { new Vector2(-475, 28), new Vector2(300, 130), new Vector2(483, -135) };
            for (int i = 0; i < _cards.Length; i++)
            {
                int index = i;
                var card = Panel("Player " + _names[i], _canvas, positions[i], new Vector2(260, 116), new Color32(42, 61, 63, 255));
                _cards[i] = card;
                var button = card.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => SelectCard(index));
                button.targetGraphic = card.GetComponent<Image>();
                var avatar = RottenEggGiftEffect.Accent("Avatar", card, new Vector2(66, 66), RottenEggAccent.Shape.Disc,
                    i == 0 ? new Color32(170, 120, 123, 255) : i == 1 ? new Color32(109, 143, 126, 255) : new Color32(169, 149, 94, 255));
                avatar.anchoredPosition = new Vector2(-82, 0);
                Label(_names[i].Substring(_names[i].Length - 1), avatar, Vector2.zero, new Vector2(60, 60), 28, Cream);
                Label(_names[i], card, new Vector2(42, 20), new Vector2(142, 35), 25, Cream, TextAlignmentOptions.Left);
                Label("筹码  " + (12500 + i * 3500).ToString("N0"), card, new Vector2(42, -20), new Vector2(142, 25), 16, Mint, TextAlignmentOptions.Left);
            }
            _selected = _cards[1];
            Label("中心弹出   →   弧线投掷   →   破裂滑落 2 秒   →   渐隐", _canvas,
                new Vector2(0, -300), new Vector2(1080, 34), 21, new Color32(154, 177, 166, 255));
            Button("再扔一次", _canvas, new Vector2(-215, -365), new Vector2(230, 54), Replay, Mint, new Color32(22, 42, 33, 255));
            _loopLabel = Button(_autoLoop ? "循环播放：开" : "循环播放：关", _canvas, new Vector2(65, -365), new Vector2(230, 54), ToggleLoop,
                new Color32(40, 60, 59, 255), Cream);
            _status = Label("就绪", _canvas, new Vector2(340, -365), new Vector2(220, 36), 19, Mint);
            var progressBg = Panel("Progress rail", _canvas, new Vector2(0, -414), new Vector2(660, 3), new Color32(45, 65, 61, 255));
            _progress = Panel("Progress", progressBg, Vector2.zero, new Vector2(0, 3), Mint);
            _progress.anchorMin = _progress.anchorMax = new Vector2(0, 0.5f);
            _progress.pivot = new Vector2(0, 0.5f);
            _menuBackdrop = Panel("Click outside to dismiss", _canvas, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.22f));
            Stretch(_menuBackdrop);
            _menuBackdrop.gameObject.AddComponent<Button>().onClick.AddListener(CloseMenu);
            _menu = Panel("Gift menu", _menuBackdrop, Vector2.zero, new Vector2(260, 164), new Color32(48, 66, 62, 255));
            _recipient = Label("送给 阿凯", _menu, new Vector2(0, 54), new Vector2(218, 32), 20, Cream);
            var icon = RottenEggGiftEffect.Picture("Gift icon", _menu, _effectPrefab.EggSprite, new Vector2(66, 66));
            icon.anchoredPosition = new Vector2(-77, -5);
            Label("臭鸡蛋", _menu, new Vector2(34, 8), new Vector2(140, 28), 22, Cream);
            Button("使用 · 预览", _menu, new Vector2(32, -37), new Vector2(150, 36), Replay, Mint, new Color32(22, 42, 33, 255));
            _menuBackdrop.gameObject.SetActive(false);
            _effect = Instantiate(_effectPrefab, _canvas);
            _effect.name = "Rotten egg effect";
            Stretch((RectTransform)_effect.transform);
        }

        private RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var rect = RottenEggGiftEffect.Node(name, parent, size);
            rect.anchoredPosition = position;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private TMP_Text Label(string text, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var rect = RottenEggGiftEffect.Node(text, parent, size);
            rect.anchoredPosition = position;
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private TMP_Text Button(string text, Transform parent, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action, Color fill, Color textColor)
        {
            var rect = Panel(text, parent, position, size, fill);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(action);
            return Label(text, rect, Vector2.zero, size, 20, textColor);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
