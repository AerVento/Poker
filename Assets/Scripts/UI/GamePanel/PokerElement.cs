using Cysharp.Threading.Tasks;
using Game.SO;
using Game.Content;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class PokerElement : MonoBehaviour
    {
        [SerializeField]
        private Image _image;

        private Poker _poker;
        private bool _isShowing = true;
        private CardFaceView _visual;
        private readonly RemoteAssetBinding<PokerCardsSO> _binding = new RemoteAssetBinding<PokerCardsSO>();

        private void OnEnable()
        {
            CardFaceAppearance.Changed += RefreshAppearance;
            RefreshAppearance();
        }

        private void OnDisable()
        {
            CardFaceAppearance.Changed -= RefreshAppearance;
            if (_visual != null) _visual.SetCards(null);
            _binding.Dispose();
        }

        private void RefreshAppearance()
        {
            var face = CardFaceAppearance.Current;
            if (_visual == null)
            {
                if (CardFaceAppearance.ViewPrefab != null)
                {
                    _visual = Instantiate(CardFaceAppearance.ViewPrefab, transform);
                    var rect = (RectTransform)_visual.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    rect.localScale = Vector3.one;
                }
            }
            if (isActiveAndEnabled || !Application.isPlaying)
                _binding.Bind(face != null ? face.Cards : null, cards =>
                {
                    if (_visual != null)
                    {
                        _visual.SetCards(cards != null ? cards : ClassicCards);
                        _visual.Display(_poker, _isShowing);
                    }
                });
            if (_image != null) _image.enabled = _visual == null;
            if (_visual != null) _visual.Display(_poker, _isShowing);
            else if (_image != null)
            {
                var cards = ClassicCards;
                _image.sprite = cards == null ? null : _isShowing ? cards.GetSprite(_poker) : cards.Back;
            }
        }

        // The bundled classic cards, used for the default face and before any remote face loads.
        private static PokerCardsSO ClassicCards => CardFaceAppearance.ClassicCards;

        /// <summary>
        /// 扑克
        /// </summary>
        public Poker Poker
        {
            get => _poker;
            set
            {
                _poker = value;
                RefreshAppearance();
            }
        }

        /// <summary>
        /// 是否可见，不可见就只会显示背面
        /// </summary>
        public bool IsShowing
        {
            get => _isShowing;
            set
            {
                _isShowing = value;
                RefreshAppearance();

            }
        }
    }
}
