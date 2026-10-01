using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Gifts
{
    /// <summary>
    /// A visual node driven by gift scripts. Position and rotation channels add up and scale and alpha channels
    /// multiply, so several overlapping tweens can move one node without nesting extra transforms.
    /// Channel 0 is the absolute base value.
    /// </summary>
    internal sealed class GiftNode
    {
        public const int Channels = 8;
        public readonly RectTransform Rect;
        private readonly Graphic _graphic;
        private CanvasGroup _group;
        private readonly Vector2[] _position = new Vector2[Channels];
        private readonly float[] _rotation = new float[Channels];
        private readonly Vector2[] _scale = new Vector2[Channels];
        private readonly float[] _alpha = new float[Channels];
        public bool Visible { get; private set; }
        public Sprite Sprite { get; private set; }
        public Image Image => _graphic as Image;
        public Graphic Graphic => _graphic;

        public GiftNode(RectTransform rect)
        {
            Rect = rect;
            _graphic = rect.GetComponent<Graphic>();
            _group = rect.GetComponent<CanvasGroup>();
            for (var i = 0; i < Channels; i++)
            {
                _scale[i] = Vector2.one;
                _alpha[i] = 1f;
            }
            _position[0] = rect.anchoredPosition;
            _rotation[0] = rect.localEulerAngles.z;
            _scale[0] = rect.localScale;
            _alpha[0] = _group != null ? _group.alpha : _graphic != null ? _graphic.color.a : 1f;
            Visible = rect.gameObject.activeSelf;
            Sprite = Image != null ? Image.sprite : null;
        }

        public void SetPosition(int channel, Vector2 value)
        {
            _position[channel] = value;
            var sum = Vector2.zero;
            for (var i = 0; i < Channels; i++) sum += _position[i];
            Rect.anchoredPosition = sum;
        }

        public void SetRotation(int channel, float value)
        {
            _rotation[channel] = value;
            var sum = 0f;
            for (var i = 0; i < Channels; i++) sum += _rotation[i];
            Rect.localRotation = Quaternion.Euler(0f, 0f, sum);
        }

        public void SetScale(int channel, Vector2 value)
        {
            _scale[channel] = value;
            var product = Vector2.one;
            for (var i = 0; i < Channels; i++) product = Vector2.Scale(product, _scale[i]);
            Rect.localScale = new Vector3(product.x, product.y, 1f);
        }

        public void SetAlpha(int channel, float value)
        {
            _alpha[channel] = value;
            // A container fades through its own group.
            if (_group == null && _graphic == null)
            {
                _group = Rect.gameObject.AddComponent<CanvasGroup>();
                _group.blocksRaycasts = _group.interactable = false;
            }
            var product = 1f;
            for (var i = 0; i < Channels; i++) product *= _alpha[i];
            if (_group != null) _group.alpha = product;
            else if (_graphic != null)
            {
                var color = _graphic.color;
                color.a = product;
                _graphic.color = color;
            }
        }

        public void SetColor(Color color)
        {
            if (_graphic == null) return;
            _graphic.color = color;
            SetAlpha(0, color.a);
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;
            Rect.gameObject.SetActive(visible);
        }

        public void SetSprite(Sprite sprite)
        {
            Sprite = sprite;
            if (Image != null) Image.sprite = sprite;
        }
    }
}
