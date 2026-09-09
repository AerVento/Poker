using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Gifts
{
    /// <summary>Local presentation only. Call Play after the server accepts a gift use.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class RottenEggGiftEffect : MonoBehaviour
    {
        [SerializeField] private Sprite _eggSprite;
        [SerializeField] private Sprite _splatSprite;
        [Header("Timing (unscaled seconds)")]
        [Min(0.01f), SerializeField] private float _appearDuration = 0.32f;
        [Min(0.01f), SerializeField] private float _flightDuration = 0.72f;
        [Min(0.01f), SerializeField] private float _slideDuration = 2f;
        [Min(0.01f), SerializeField] private float _fadeDuration = 0.65f;
        [Header("Size in canvas units")]
        [Min(1f), SerializeField] private float _eggSize = 180f;
        [Min(0f), SerializeField] private float _arcHeight = 200f;
        [Min(0f), SerializeField] private float _slideDistance = 52f;
        private RectTransform _root;
        private RectTransform _visual;
        private RectTransform _egg;
        private RectTransform _impact;
        private RectTransform _splat;
        private RectTransform _halo;
        private RectTransform _ring;
        private CanvasGroup _group;
        private CanvasGroup _impactGroup;
        private RectTransform _target;
        private Vector2 _origin;
        private float _elapsed;
        private bool _preview;
        private readonly RectTransform[] _drops = new RectTransform[7];
        private readonly RectTransform[] _wisps = new RectTransform[3];
        private readonly RectTransform[] _flecks = new RectTransform[10];
        private readonly RectTransform[] _trail = new RectTransform[7];
        public bool IsPlaying { get; private set; }
        public float Duration => _appearDuration + _flightDuration + _slideDuration + _fadeDuration;
        public float Elapsed => _elapsed;
        public string Phase => !IsPlaying ? "就绪" : _elapsed < _appearDuration ? "弹出" :
            _elapsed < _appearDuration + _flightDuration ? "抛物线飞行" :
            _elapsed < _appearDuration + _flightDuration + _slideDuration ? "破裂 · 滑落" : "渐隐";
        public Sprite EggSprite => _eggSprite;

        public void Play(RectTransform target)
        {
            Stop();
            if (target == null || !target.gameObject.activeInHierarchy || !isActiveAndEnabled)
                return;
            Build();
            _target = target;
            _origin = ToLocal(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            _elapsed = 0f;
            _preview = false;
            IsPlaying = true;
            _visual.gameObject.SetActive(true);
            Evaluate(0f);
        }

        // Deterministic pose for art review / timeline inspection, with no coroutine to leak.
        public void PreviewAt(RectTransform target, float seconds)
        {
            Play(target);
            if (!IsPlaying) return;
            _preview = true;
            _elapsed = Mathf.Clamp(seconds, 0f, Duration);
            Evaluate(_elapsed);
        }

        public void Stop()
        {
            IsPlaying = false;
            _target = null;
            if (_visual != null) _visual.gameObject.SetActive(false);
        }

        private void OnDisable() => Stop();

        private void LateUpdate()
        {
            if (!IsPlaying) return;
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }
            if (!_preview) _elapsed += Time.unscaledDeltaTime;
            Evaluate(_elapsed);
            if (!_preview && _elapsed >= Duration) Stop();
        }

        private Vector2 ToLocal(Vector2 screen)
        {
            var canvas = _root.GetComponentInParent<Canvas>().rootCanvas;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var point);
            return point;
        }

        private Vector2 TargetPoint()
        {
            var canvas = _target.GetComponentInParent<Canvas>();
            var camera = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.rootCanvas.worldCamera;
            return ToLocal(RectTransformUtility.WorldToScreenPoint(camera, _target.TransformPoint(_target.rect.center)));
        }

        private Vector2 Flight(float t, Vector2 end)
        {
            return Vector2.LerpUnclamped(_origin, end, t) + Vector2.up * (4f * _arcHeight * t * (1f - t));
        }

        private void Evaluate(float time)
        {
            Vector2 end = TargetPoint();
            float hitTime = _appearDuration + _flightDuration;
            float hit = time - hitTime;
            _group.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((hit - _slideDuration) / _fadeDuration));
            _egg.gameObject.SetActive(hit < 0f);
            _impact.gameObject.SetActive(hit >= 0f);
            _halo.gameObject.SetActive(time < _appearDuration);
            float appear = Mathf.Clamp01(time / _appearDuration);
            float pop = 1f + 2.70158f * Mathf.Pow(appear - 1f, 3f) + 1.70158f * Mathf.Pow(appear - 1f, 2f);
            _halo.anchoredPosition = _origin;
            _halo.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.6f, appear);
            SetAlpha(_halo, (1f - appear) * 0.3f);
            _egg.anchoredPosition = _origin;
            _egg.localScale = Vector3.one * Mathf.Max(0f, pop);
            _egg.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(appear * Mathf.PI) * -12f);
            float flight = Mathf.Clamp01((time - _appearDuration) / _flightDuration);
            if (time >= _appearDuration && hit < 0f)
            {
                _egg.anchoredPosition = Flight(flight, end);
                _egg.localScale = Vector3.one * Mathf.Lerp(1f, 0.73f, flight);
                _egg.localRotation = Quaternion.Euler(0f, 0f, -420f * flight);
            }
            for (int i = 0; i < _trail.Length; i++)
            {
                float t = flight - 0.027f * (i + 1);
                bool visible = time >= _appearDuration && hit < 0f && t >= 0f;
                _trail[i].gameObject.SetActive(visible);
                if (!visible) continue;
                _trail[i].anchoredPosition = Flight(t, end);
                _trail[i].localScale = Vector3.one * (1f - i * 0.10f);
                SetAlpha(_trail[i], 0.30f * (1f - i / 7f));
            }
            if (hit < 0f) return;
            _impact.anchoredPosition = end;
            float slide = Mathf.Clamp01(hit / _slideDuration);
            float settle = 1f + Mathf.Sin(Mathf.Clamp01(hit / 0.32f) * Mathf.PI * 2f) * 0.16f * Mathf.Exp(-hit * 6f);
            _splat.localScale = new Vector3(settle, 1f / settle, 1f);
            _splat.anchoredPosition = Vector2.down * (_slideDistance * slide * slide);
            _splat.localRotation = Quaternion.Euler(0f, 0f, -6f * slide);
            _impactGroup.alpha = Mathf.Clamp01(hit / 0.045f);
            _ring.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.65f, Mathf.Clamp01(hit / 0.32f));
            SetAlpha(_ring, (1f - Mathf.Clamp01(hit / 0.32f)) * 0.85f);
            for (int i = 0; i < _drops.Length; i++)
            {
                float flow = Mathf.Clamp01((hit - i * 0.08f) / _slideDuration);
                float length = (30f + (i % 3) * 18f) * flow;
                _drops[i].sizeDelta = new Vector2(9f + i % 3 * 3f, Mathf.Max(1f, length));
                _drops[i].anchoredPosition = new Vector2((i - 3) * 19f, -24f - length * 0.5f - _slideDistance * slide * slide);
                SetAlpha(_drops[i], Mathf.Clamp01(hit * 5f));
            }
            for (int i = 0; i < _wisps.Length; i++)
            {
                float cycle = Mathf.Repeat(hit * 0.56f + i * 0.30f, 1f);
                _wisps[i].anchoredPosition = new Vector2((i - 1) * 52f + Mathf.Sin(hit * 3f + i) * 9f, 32f + cycle * 85f - slide * 14f);
                _wisps[i].localScale = new Vector3(1f + cycle * 0.4f, 0.7f + cycle * 0.5f, 1f);
                SetAlpha(_wisps[i], Mathf.Sin(cycle * Mathf.PI) * 0.7f * Mathf.Clamp01(hit * 5f));
            }
            for (int i = 0; i < _flecks.Length; i++)
            {
                float a = i * Mathf.PI * 2f / _flecks.Length;
                float speed = 95f + (i % 3) * 32f;
                float t = Mathf.Min(hit, 0.65f);
                _flecks[i].anchoredPosition = new Vector2(Mathf.Cos(a) * speed * t,
                    Mathf.Sin(a) * speed * t - 150f * t * t);
                _flecks[i].localRotation = Quaternion.Euler(0, 0, i * 37f + t * 230f);
                SetAlpha(_flecks[i], 1f - Mathf.Clamp01((hit - 0.22f) / 0.4f));
            }
        }

        private void Build()
        {
            if (_visual != null) return;
            _root = (RectTransform)transform;
            _visual = Node("Visual", _root, Vector2.zero);
            var canvas = _visual.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            _group = _visual.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _halo = Accent("Pop glow", _visual, new Vector2(230f, 230f), RottenEggAccent.Shape.Disc, new Color(0.75f, 1f, 0.4f));
            for (int i = 0; i < _trail.Length; i++)
                _trail[i] = Accent("Trail " + i, _visual, new Vector2(25f, 25f), RottenEggAccent.Shape.Disc, new Color(0.78f, 0.93f, 0.40f));
            _egg = Picture("Intact egg", _visual, _eggSprite, new Vector2(_eggSize, _eggSize));
            _impact = Node("Impact", _visual, Vector2.zero);
            _impactGroup = _impact.gameObject.AddComponent<CanvasGroup>();
            _ring = Accent("Impact ring", _impact, new Vector2(230f, 180f), RottenEggAccent.Shape.Ring, new Color(0.88f, 1f, 0.55f));
            for (int i = 0; i < _drops.Length; i++)
                _drops[i] = Accent("Sliding goo " + i, _impact, new Vector2(12, 1), RottenEggAccent.Shape.Drop,
                    i % 2 == 0 ? new Color(0.63f, 0.76f, 0.22f) : new Color(0.77f, 0.87f, 0.35f));
            _splat = Picture("Broken egg", _impact, _splatSprite, new Vector2(254f, 254f));
            for (int i = 0; i < _flecks.Length; i++)
                _flecks[i] = Accent("Flying fragment " + i, _impact, new Vector2(10f + i % 3 * 3f, 16f), RottenEggAccent.Shape.Drop,
                    i % 2 == 0 ? new Color(0.98f, 0.91f, 0.70f) : new Color(0.7f, 0.83f, 0.29f));
            for (int i = 0; i < _wisps.Length; i++)
                _wisps[i] = Accent("Stink wisp " + i, _impact, new Vector2(40f, 85f), RottenEggAccent.Shape.Wisp, new Color(0.66f, 0.84f, 0.27f));
        }

        private static void SetAlpha(RectTransform rect, float alpha)
        {
            var graphic = rect.GetComponent<Graphic>();
            var tint = graphic.color;
            tint.a = alpha;
            graphic.color = tint;
        }

        internal static RectTransform Node(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }

        internal static RectTransform Picture(string name, Transform parent, Sprite sprite, Vector2 size)
        {
            var rect = Node(name, parent, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }

        internal static RectTransform Accent(string name, Transform parent, Vector2 size, RottenEggAccent.Shape shape, Color color)
        {
            var rect = Node(name, parent, size);
            var accent = rect.gameObject.AddComponent<RottenEggAccent>();
            accent.Kind = shape;
            accent.color = color;
            accent.raycastTarget = false;
            return rect;
        }
    }
}
