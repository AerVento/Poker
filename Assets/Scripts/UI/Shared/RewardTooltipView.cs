using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class RewardTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform _window;
        [SerializeField] private TextMeshProUGUI _name;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private Button _dismiss;
        [SerializeField] private ScrollRect _scroll;
        private RectTransform _anchor;
        private float _availableHeight = -1f;
        private readonly Vector3[] _corners = new Vector3[4];
        private void OnEnable() { _dismiss.onClick.AddListener(Close); }
        private void OnDisable() { _dismiss.onClick.RemoveListener(Close); }
        private void Close() { gameObject.SetActive(false); Destroy(gameObject); }
        public void Show(RectTransform anchor, string name, string description)
        {
            _anchor = anchor;
            _name.text = name;
            _description.text = description;
            _availableHeight = -1f;
            Position();
        }
        private void LateUpdate() => Position();

        private void Resize(float availableHeight)
        {
            if (Mathf.Approximately(_availableHeight, availableHeight)) return;
            _availableHeight = availableHeight;
            float width = _window.rect.width - 56f;
            float nameHeight = Mathf.Ceil(_name.GetPreferredValues(_name.text, width, Mathf.Infinity).y);
            float descriptionHeight = Mathf.Ceil(_description.GetPreferredValues(_description.text, width, Mathf.Infinity).y);
            float heading = 20f + nameHeight + (descriptionHeight > 0 ? 12f : 0f);
            float height = Mathf.Min(heading + descriptionHeight + 24f, availableHeight);
            _window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            _name.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nameHeight);
            var scrollRect = (RectTransform)_scroll.transform;
            scrollRect.anchoredPosition = new Vector2(0f, -heading);
            scrollRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, height - heading - 24f));
            _scroll.vertical = descriptionHeight > scrollRect.rect.height + 0.5f;
            _scroll.StopMovement();
            _scroll.content.anchoredPosition = Vector2.zero;
        }
        private void Position()
        {
            if (_anchor == null || !_anchor.gameObject.activeInHierarchy) { Close(); return; }
            var root = (RectTransform)transform;
            float scale = Mathf.Min(root.rect.width / 1920f, root.rect.height / 1080f);
            _window.localScale = Vector3.one * scale;
            if (scale <= 0f) return;
            Resize(Mathf.Min(640f, (root.rect.height - 24f) / scale));
            _anchor.GetWorldCorners(_corners);
            var right = (Vector2)root.InverseTransformPoint(_corners[2]);
            var left = (Vector2)root.InverseTransformPoint(_corners[1]);
            Vector2 size = _window.rect.size * scale;
            float x = right.x + 16f * scale;
            if (x + size.x > root.rect.xMax - 12f) x = left.x - size.x - 16f * scale;
            x = Mathf.Clamp(x, root.rect.xMin + 12f, root.rect.xMax - size.x - 12f);
            float y = Mathf.Clamp(right.y, root.rect.yMin + size.y + 12f, root.rect.yMax - 12f);
            _window.anchoredPosition = new Vector2(x, y);
        }
    }
}
