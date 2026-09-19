using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.UI.GamePanelElement
{
    public sealed class PlayerListItemStateView : MonoBehaviour
    {
        private struct FontState
        {
            public float Size;
            public bool AutoSizing;
            public float Minimum;
            public float Maximum;
        }

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _time;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _chips;
        [SerializeField] private GameObject _spectatorIcon;
        [SerializeField] private GameObject _foldIcon;
        [SerializeField] private List<GameObject> _operationIcons = new List<GameObject>();
        [SerializeField, Range(0f, 1f)] private float _inactiveAlpha = 0.4f;
        private float _nameFontSize;
        private bool _nameAutoSizing;
        private float _nameFontSizeMin;
        private float _nameFontSizeMax;
        private bool? _namePortrait;
        private float _nameCanvasScale = -1f;
        private readonly Dictionary<TMP_Text, FontState> _auxiliaryFonts =
            new Dictionary<TMP_Text, FontState>();

        private void Awake()
        {
            if (_name != null)
            {
                _nameFontSize = _name.fontSize;
                _nameAutoSizing = _name.enableAutoSizing;
                _nameFontSizeMin = _name.fontSizeMin;
                _nameFontSizeMax = _name.fontSizeMax;
                _name.textWrappingMode = TextWrappingModes.NoWrap;
                _name.overflowMode = TextOverflowModes.Ellipsis;
            }
            CaptureAuxiliaryFont(_chips);
            CaptureAuxiliaryFont(_time);
            foreach (var icon in _operationIcons)
            {
                if (icon == null)
                    continue;
                foreach (var text in icon.GetComponentsInChildren<TMP_Text>(true))
                    CaptureAuxiliaryFont(text);
            }
        }

        public void Present(bool spectator, bool folded)
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = spectator || folded ? _inactiveAlpha : 1f;
            if (_time != null && (spectator || folded))
                _time.gameObject.SetActive(false);
            if (_chips != null)
                _chips.gameObject.SetActive(!spectator);
            if (_spectatorIcon != null)
                _spectatorIcon.SetActive(spectator);
            ApplyNameSizing();

            if (!spectator && !folded)
                return;

            foreach (var icon in _operationIcons)
            {
                if (icon != null)
                    icon.SetActive(false);
            }

            if (_foldIcon != null)
                _foldIcon.SetActive(folded);
        }

        private void LateUpdate()
        {
            if (_name == null)
                return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return;
            var canvasRect = canvas.rootCanvas.transform as RectTransform;
            bool portrait = canvasRect != null && canvasRect.rect.height > canvasRect.rect.width;
            float scale = Mathf.Max(0.01f, canvas.rootCanvas.scaleFactor);
            float target = portrait ? Mathf.Max(_nameFontSize, 12f / scale) : _nameFontSize;
            if (_namePortrait != portrait || !Mathf.Approximately(_nameCanvasScale, scale) ||
                portrait && (_name.enableAutoSizing || !Mathf.Approximately(_name.fontSize, target)))
                ApplyNameSizing();
        }

        private void ApplyNameSizing()
        {
            if (_name == null)
                return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return;
            var canvasRect = canvas.rootCanvas.transform as RectTransform;
            bool portrait = canvasRect != null && canvasRect.rect.height > canvasRect.rect.width;
            float scale = Mathf.Max(0.01f, canvas.rootCanvas.scaleFactor);
            if (portrait)
            {
                float size = Mathf.Max(_nameFontSize, 12f / scale);
                _name.enableAutoSizing = false;
                _name.fontSizeMin = size;
                _name.fontSizeMax = size;
                _name.fontSize = size;
            }
            else
            {
                _name.enableAutoSizing = _nameAutoSizing;
                _name.fontSizeMin = _nameFontSizeMin;
                _name.fontSizeMax = _nameFontSizeMax;
                _name.fontSize = _nameFontSize;
            }
            _namePortrait = portrait;
            _nameCanvasScale = scale;
            ApplyAuxiliarySizing(portrait, scale);
        }

        private void CaptureAuxiliaryFont(TMP_Text text)
        {
            if (text == null || text == _name || _auxiliaryFonts.ContainsKey(text))
                return;
            _auxiliaryFonts.Add(text, new FontState
            {
                Size = text.fontSize,
                AutoSizing = text.enableAutoSizing,
                Minimum = text.fontSizeMin,
                Maximum = text.fontSizeMax,
            });
        }

        private void ApplyAuxiliarySizing(bool portrait, float scale)
        {
            foreach (var pair in _auxiliaryFonts)
            {
                if (pair.Key == null)
                    continue;
                var text = pair.Key;
                var state = pair.Value;
                if (portrait)
                {
                    float pixels = text == _chips ? 11f : 10f;
                    float size = Mathf.Max(state.Size, pixels / scale);
                    text.enableAutoSizing = false;
                    text.fontSizeMin = size;
                    text.fontSizeMax = size;
                    text.fontSize = size;
                }
                else
                {
                    text.enableAutoSizing = state.AutoSizing;
                    text.fontSizeMin = state.Minimum;
                    text.fontSizeMax = state.Maximum;
                    text.fontSize = state.Size;
                }
            }
        }
    }
}
