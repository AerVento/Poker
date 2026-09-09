using Cysharp.Threading.Tasks;
using Framework.SO;
using Game.SO;
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
        private CardFaceProductSO _appearance;

        private void OnEnable()
        {
            CardFaceAppearance.Changed += RefreshAppearance;
            RefreshAppearance();
        }

        private void OnDisable() => CardFaceAppearance.Changed -= RefreshAppearance;

        private void RefreshAppearance()
        {
            var product = CardFaceAppearance.Current;
            if (_appearance != product || _visual == null)
            {
                if (_visual != null) { _visual.gameObject.SetActive(false); Destroy(_visual.gameObject); }
                _appearance = product;
                if (product != null && product.Prefab != null)
                {
                    _visual = Instantiate(product.Prefab, transform);
                    var rect = (RectTransform)_visual.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    rect.localScale = Vector3.one;
                }
            }
            if (_image != null) _image.enabled = _visual == null;
            if (_visual != null) _visual.Display(_poker, _isShowing);
            else if (_image != null) _image.sprite = _isShowing ? GetSO().GetSprite(_poker) : GetSO().Back;
        }

        private PokerCardsSO GetSO() => SingletonSOManager.Instance.GetSOFile<PokerCardsSO>();

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
