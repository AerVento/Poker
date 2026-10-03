using Game.Content;
using Game.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // A custom card replaces the classic surface; state indicators live inside its safe area.
    public sealed class NamecardAppearance : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _artwork;
        [SerializeField] private Image _outline;
        [Tooltip("Content aligned to the selected artwork's safe area, without resizing the player slot.")]
        [SerializeField] private RectTransform _content;
        private bool _highlighted;
        private bool _hasPresented;
        private NamecardDefinition _card;
        private string _equippedId;
        private ulong _serviceYears;
        private TMP_Text _playerName;
        private Vector2 _lastArtworkSize;
        private NamecardArtworkSO _loadedArtwork;
        private readonly RemoteAssetBinding<NamecardArtworkSO> _binding = new RemoteAssetBinding<NamecardArtworkSO>();

        private readonly InfernalNameStyle _nameStyle = new InfernalNameStyle();

        public void Present(string id, ulong serviceYears = 0, TMP_Text playerName = null)
        {
            _nameStyle.Present(playerName, serviceYears);
            _equippedId = id;
            _serviceYears = serviceYears;
            _playerName = playerName;
            _hasPresented = true;
            Resolve();
            BindArtwork();
        }

        // Service namecards replace the equipped one; both come from the catalog.
        private void Resolve()
        {
            var catalog = ClientCatalog.Current;
            _card = catalog?.FindNamecard(catalog.ResolveNamecard(_equippedId, _serviceYears));
        }

        // Rooms can be drawn before the catalog arrives; resolve the card again once it does.
        private void OnCatalogChanged()
        {
            if (!_hasPresented) return;
            _nameStyle.Present(_playerName, _serviceYears);
            Resolve();
            BindArtwork();
        }

        private void OnDestroy() { _binding.Dispose(); _nameStyle.Dispose(); }

        private void OnDisable()
        {
            ClientCatalog.Changed -= OnCatalogChanged;
            _loadedArtwork = null;
            if (_artwork != null) _artwork.sprite = null;
            _binding.Dispose();
        }

        private void BindArtwork()
        {
            if (Application.isPlaying && !isActiveAndEnabled) return;
            _binding.Bind(_card != null ? _card.Artwork : null, artwork =>
            {
                _loadedArtwork = artwork;
                RefreshArtwork();
            });
            RefreshArtwork();
        }

        private void OnEnable()
        {
            ClientCatalog.Changed += OnCatalogChanged;
            if (_hasPresented)
                BindArtwork();
        }

        private void LateUpdate()
        {
            // Layout groups can resize the child artwork after its parent's callbacks have run.
            if (_hasPresented && _artwork != null && _lastArtworkSize != _artwork.rectTransform.rect.size)
                RefreshArtwork();
        }

        private void RefreshArtwork()
        {
            if (_artwork == null)
                return;
            _lastArtworkSize = _artwork.rectTransform.rect.size;
            float aspect = _lastArtworkSize.y > 0f ? _lastArtworkSize.x / _lastArtworkSize.y : NamecardLayout.Aspect;
            var sprite = _card != null && !_card.IsDefault && _loadedArtwork != null ? _loadedArtwork.Select(aspect) : null;
            bool custom = sprite != null;
            _artwork.sprite = sprite;
            _artwork.type = Image.Type.Simple;
            _artwork.preserveAspect = true;
            _artwork.gameObject.SetActive(custom);
            if (_background != null)
                _background.enabled = !custom;
            if (_outline != null)
                _outline.gameObject.SetActive(_highlighted);
            FitContent(sprite);
        }

        private void FitContent(Sprite sprite)
        {
            if (_content == null)
                return;
            if (sprite == null)
            {
                NamecardLayout.FitSafeArea(_content);
                return;
            }
            var parent = _content.parent as RectTransform;
            if (parent == null || parent.rect.width <= 0f || parent.rect.height <= 0f ||
                _lastArtworkSize.x <= 0f || _lastArtworkSize.y <= 0f)
                return;

            float aspect = sprite.rect.width / sprite.rect.height;
            Vector2 size = _lastArtworkSize;
            if (size.x / size.y > aspect)
                size.x = size.y * aspect;
            else
                size.y = size.x / aspect;
            var inset = _loadedArtwork != null && sprite == _loadedArtwork.Compact ? NamecardLayout.CompactSafeMin : NamecardLayout.SafeMin;
            var halfContent = Vector2.Scale(size, Vector2.one * 0.5f - inset);
            var artworkRect = _artwork.rectTransform;
            Vector2 min = parent.InverseTransformPoint(artworkRect.TransformPoint(artworkRect.rect.center - halfContent));
            Vector2 max = parent.InverseTransformPoint(artworkRect.TransformPoint(artworkRect.rect.center + halfContent));
            _content.anchorMin = new Vector2((min.x - parent.rect.xMin) / parent.rect.width, (min.y - parent.rect.yMin) / parent.rect.height);
            _content.anchorMax = new Vector2((max.x - parent.rect.xMin) / parent.rect.width, (max.y - parent.rect.yMin) / parent.rect.height);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;
        }

        public void SetHighlighted(bool highlighted)
        {
            _highlighted = highlighted;
            if (_outline != null)
                _outline.gameObject.SetActive(highlighted);
        }
    }
}
