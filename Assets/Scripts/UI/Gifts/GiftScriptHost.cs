using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Game.Content;
using UnityEngine;
using UnityEngine.UI;
using XLua;
using Object = UnityEngine.Object;

namespace Game.UI.Gifts
{
    /// <summary>
    /// The C# side of one gift playback. Only the trusted bootstrap in GiftScriptRuntime calls it; gift scripts get
    /// a facade of plain Lua functions and integer node handles, never this object. Every public member is part of
    /// the content format. Positions are in the effect root's local space; card positions are read once at build.
    /// </summary>
    [LuaCallCSharp, ContentScriptApi]
    public sealed class GiftScriptHost
    {
        public const int ApiVersion = 1;
        public const int MaxNodes = 160;
        public const int MaxTweens = 800;
        public const float MaxDuration = 10f;

        private sealed class TweenSpec
        {
            public float At;
            public float Duration;
            public Ease Ease;
            public Action<float> Apply;
            public int Loops = 1;
            public LoopType LoopType = LoopType.Restart;
        }

        private sealed class Step
        {
            public GiftNode Node;
            public float At;
            public bool Visible;
            public Sprite Sprite;
            public bool IsSprite;
        }

        private readonly RectTransform _root;
        private readonly RectTransform _sender;
        private readonly RectTransform _target;
        private readonly Func<string, Object> _resource;
        private readonly System.Random _random;
        private readonly List<GiftNode> _nodes = new List<GiftNode>();
        private readonly List<TweenSpec> _tweens = new List<TweenSpec>();
        private readonly List<Step> _steps = new List<Step>();
        private readonly List<(float At, AudioClip Clip, float Volume)> _sounds = new List<(float, AudioClip, float)>();
        private readonly List<(float At, LuaFunction Function)> _callbacks = new List<(float, LuaFunction)>();
        private readonly List<(ChampagneGlassGraphic Graphic, float Offset)> _clocks = new List<(ChampagneGlassGraphic, float)>();
        private bool _finished;
        private Sequence _timeline;
        private Canvas _concurrencyScope;
        private readonly HashSet<GiftScriptHost> _countedHosts = new HashSet<GiftScriptHost>();
        private readonly Dictionary<string, int> _concurrentCounts = new Dictionary<string, int>();
        internal Func<bool> AudioEnabled { get; set; }
        internal Func<IDisposable> RetainAudioResources { get; set; }
        private readonly List<LuaFunction> _stopCallbacks = new List<LuaFunction>();
        private readonly List<LuaFunction> _audioStopCallbacks = new List<LuaFunction>();
        private readonly HashSet<int> _musicHandles = new HashSet<int>();
        private Func<bool> _muted;
        private bool _audioStopped;
        private bool _disposed;
        private sealed class ConcurrentBinding
        {
            public GiftScriptHost Host;
            public GiftNode Node;
            public string Key;
            public float Start, End, Growth, Limit;
        }
        private static readonly List<ConcurrentBinding> ConcurrentBindings = new List<ConcurrentBinding>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetConcurrentBindings() => ConcurrentBindings.Clear();

        // One host counts once, even when several colored beam layers share the binding.
        public void ConcurrentScale(int node, string key, float at, float duration, float growth, float limit)
        {
            CheckEnd(at);
            CheckEnd(at + duration);
            if (string.IsNullOrEmpty(key) || duration <= 0f || !IsFinite(growth) || !IsFinite(limit) || growth < 0f || limit < 1f || limit > 4f)
                throw new ArgumentException("Invalid concurrent scale.");
            if (ConcurrentBindings.Any(binding => binding.Host == this && binding.Node == NodeAt(node)))
                throw new ArgumentException("Node already has a concurrent scale binding.");
            ConcurrentBindings.Add(new ConcurrentBinding { Host = this, Node = NodeAt(node), Key = key,
                Start = at, End = at + duration, Growth = growth, Limit = limit });
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void UpdateConcurrentScales()
        {
            _concurrentCounts.Clear();
            foreach (var binding in ConcurrentBindings)
            {
                if (binding.Host != this) continue;
                if (!_concurrentCounts.TryGetValue(binding.Key, out int count))
                {
                    _countedHosts.Clear();
                    foreach (var other in ConcurrentBindings)
                    {
                        var host = other.Host;
                        if (other.Key != binding.Key || host._root == null || host._timeline == null || !host._timeline.IsActive()) continue;
                        if (host._concurrencyScope != _concurrencyScope) continue;
                        float elapsed = host._timeline.Elapsed();
                        if (elapsed >= other.Start && elapsed < other.End && _countedHosts.Add(host)) count++;
                    }
                    _concurrentCounts[binding.Key] = count;
                }
                binding.Node.SetScale(7, new Vector2(1f, Mathf.Min(binding.Limit, 1f + binding.Growth * Mathf.Max(0, count - 1))));
            }
        }

        public int Curve(int node, float at, float duration, float x0, float y0, float x1, float y1,
            float x2, float y2, float x3, float y3, string ease)
        {
            var target = NodeAt(node);
            var a = new Vector2(x0, y0); var b = new Vector2(x1, y1);
            var c = new Vector2(x2, y2); var d = new Vector2(x3, y3);
            return AddTween(at, duration, ease, t =>
            {
                float u = 1f - t;
                target.SetPosition(0, u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
            });
        }

        public void OnStop(LuaFunction function) => AddCleanup(_stopCallbacks, function);
        public void OnAudioStop(LuaFunction function) => AddCleanup(_audioStopCallbacks, function);

        private void AddCleanup(List<LuaFunction> callbacks, LuaFunction function)
        {
            if (_finished || _disposed || function == null || callbacks.Count >= 16)
                throw new ArgumentException("Invalid cleanup callback.");
            callbacks.Add(function);
        }

        // Playback handles carry no music policy: Lua decides which requests share or replace a track.
        public int MusicStart(string clip, float volume, bool loop)
        {
            if (!CanStartMusic) return 0;
            if (!IsFinite(volume)) throw new ArgumentException("Invalid music gain.");
            var handle = GiftMusicPlayback.Start(Resource<AudioClip>(clip), Mathf.Clamp01(volume), loop, this, RetainAudioResources);
            if (handle != 0) _musicHandles.Add(handle);
            return handle;
        }

        public bool MusicAlive(int handle) => GiftMusicPlayback.Find(handle) != null;

        public bool MusicRetain(int handle)
        {
            if (!CanStartMusic) return false;
            var playback = GiftMusicPlayback.Find(handle);
            if (playback == null) return false;
            playback.Retain(this);
            _musicHandles.Add(handle);
            return true;
        }

        public void MusicFade(int handle, float volume, float seconds, bool stop)
        {
            if (!IsFinite(volume) || !IsFinite(seconds) || seconds < 0f || seconds > MaxDuration)
                throw new ArgumentException("Invalid music fade.");
            if (_musicHandles.Contains(handle)) GiftMusicPlayback.Find(handle)?.Fade(Mathf.Clamp01(volume), seconds, stop);
        }

        private bool CanStartMusic => !_disposed && !_audioStopped && _finished &&
            _muted != null && !_muted() && AudioEnabled != null && AudioEnabled();

        internal void EndAudio()
        {
            if (_audioStopped) return;
            _audioStopped = true;
            RunCleanup(_audioStopCallbacks);
            foreach (var handle in _musicHandles) GiftMusicPlayback.Find(handle)?.Release(this);
            _musicHandles.Clear();
        }

        private void RunCleanup(List<LuaFunction> callbacks)
        {
            foreach (var function in callbacks)
            {
                try { if (_finished && _muted != null && !_muted()) GiftScriptRuntime.Call(function); }
                catch (Exception exception) { Debug.LogWarning("Gift cleanup failed: " + exception.Message); }
                finally { function.Dispose(); }
            }
            callbacks.Clear();
        }

        internal GiftScriptHost(RectTransform root, RectTransform layer, RectTransform sender, RectTransform target,
            Func<string, Object> resource, int seed)
        {
            _root = root;
            _concurrencyScope = root.GetComponentInParent<Canvas>().rootCanvas;
            _sender = sender;
            _target = target;
            _resource = resource;
            _random = new System.Random(seed);
            _nodes.Add(new GiftNode(layer));
        }

        // ---- Geometry, read once when the playback is built ----

        public void SenderPoint(float offsetX, float offsetY, out float x, out float y) => Point(_sender, offsetX, offsetY, out x, out y);
        public void TargetPoint(float offsetX, float offsetY, out float x, out float y) => Point(_target, offsetX, offsetY, out x, out y);
        public void SenderSize(out float width, out float height) => Size(_sender, out width, out height);
        public void TargetSize(out float width, out float height) => Size(_target, out width, out height);

        /// <summary>A point at a fraction (0-1) of the screen, in the effect root's local space.</summary>
        public void ScreenPoint(float u, float v, out float x, out float y)
        {
            var canvas = _root.GetComponentInParent<Canvas>().rootCanvas;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, new Vector2(Screen.width * u, Screen.height * v),
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
            x = local.x;
            y = local.y;
        }

        public void Bounds(out float xMin, out float yMin, out float xMax, out float yMax)
        {
            var rect = _root.rect;
            xMin = rect.xMin;
            yMin = rect.yMin;
            xMax = rect.xMax;
            yMax = rect.yMax;
        }

        /// <summary>Uniform in [0, 1). Live playback is unseeded; previews use a fixed seed.</summary>
        public float Random() => (float)_random.NextDouble();

        // ---- Nodes; 0 is the effect layer ----

        public int Node(int parent, string name, float width, float height)
        {
            var rect = CreateRect(parent, name, width, height);
            return Add(rect);
        }

        public int Image(int parent, string name, string sprite, float width, float height)
        {
            var rect = CreateRect(parent, name, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = string.IsNullOrEmpty(sprite) ? null : Resource<Sprite>(sprite);
            image.raycastTarget = false;
            return Add(rect);
        }

        public void Joint(int node, float reach, float direction)
        {
            var target = NodeAt(node);
            if (target.Image == null) throw new ArgumentException("Joint requires an image node.");
            if (float.IsNaN(reach) || float.IsInfinity(reach) || float.IsNaN(direction) || float.IsInfinity(direction) || reach <= 0f)
                throw new ArgumentException("Invalid joint reach or direction.");
            target.ConfigureJoint(reach, direction);
        }

        public int ScreenEffect(int parent, string material)
        {
            ScreenPoint(0f, 0f, out var left, out var bottom);
            ScreenPoint(1f, 1f, out var right, out var top);
            var rect = CreateRect(parent, "Frozen screen", right - left, top - bottom);
            rect.anchoredPosition = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
            rect.gameObject.AddComponent<RawImage>();
            rect.gameObject.AddComponent<GiftScreenEffect>().Configure(Resource<Material>(material), _nodes[0].Rect.GetComponent<Canvas>());
            rect.SetAsFirstSibling();
            return Add(rect);
        }

        public int ScreenParameter(int node, string parameter, float at, float duration, float from, float to, string ease)
        {
            var effect = NodeAt(node).Rect.GetComponent<GiftScreenEffect>();
            if (effect == null) throw new ArgumentException("Node is not a screen effect.");
            effect.SetParameter(parameter, from);
            return AddTween(at, duration, ease, t => effect.SetParameter(parameter, Mathf.LerpUnclamped(from, to, t)));
        }

        public int Tint(int node, float at, float duration, float r0, float g0, float b0, float r1, float g1, float b1, string ease)
        {
            var graphic = NodeAt(node).Graphic;
            if (graphic == null) throw new ArgumentException("Tint requires a graphic node.");
            var from = new Color(r0, g0, b0);
            var to = new Color(r1, g1, b1);
            return AddTween(at, duration, ease, t =>
            {
                var color = Color.LerpUnclamped(from, to, t);
                color.a = graphic.color.a;
                graphic.color = color;
            });
        }

        public int Shape(int parent, string name, string kind, float width, float height)
        {
            var rect = CreateRect(parent, name, width, height);
            Graphic graphic;
            switch (kind)
            {
                case "disc": graphic = Accent(rect, RottenEggAccent.Shape.Disc); break;
                case "drop": graphic = Accent(rect, RottenEggAccent.Shape.Drop); break;
                case "wisp": graphic = Accent(rect, RottenEggAccent.Shape.Wisp); break;
                case "ring": graphic = Accent(rect, RottenEggAccent.Shape.Ring); break;
                case "hole": graphic = Gunfire(rect, GunfireAccent.Shape.Hole); break;
                case "flash": graphic = Gunfire(rect, GunfireAccent.Shape.Flash); break;
                case "butterfly":
                case "knife":
                case "star":
                case "beam":
                case "cannon":
                case "orb":
                    var bullet = rect.gameObject.AddComponent<SpellBulletGraphic>();
                    bullet.Kind = (SpellBulletGraphic.Shape)Enum.Parse(typeof(SpellBulletGraphic.Shape), kind, true);
                    graphic = bullet;
                    break;
                case "glass":
                    var glass = rect.gameObject.AddComponent<ChampagneGlassGraphic>();
                    // The glass mesh has its own colors; fading goes through a group.
                    var group = rect.gameObject.AddComponent<CanvasGroup>();
                    group.blocksRaycasts = group.interactable = false;
                    _clocks.Add((glass, 0f));
                    graphic = glass;
                    break;
                default: throw new ArgumentException("Unknown shape: " + kind);
            }
            graphic.raycastTarget = false;
            return Add(rect);
        }

        public int Instantiate(int parent, string prefab)
        {
            var source = Resource<GameObject>(prefab);
            var instance = Object.Instantiate(source, NodeAt(parent).Rect, false);
            if (!(instance.transform is RectTransform rect))
            {
                Object.Destroy(instance);
                throw new ArgumentException("Gift prefab must be a UI object: " + prefab);
            }
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            foreach (var glass in instance.GetComponentsInChildren<ChampagneGlassGraphic>(true)) _clocks.Add((glass, 0f));
            return Add(rect);
        }

        public int Find(int node, string path)
        {
            var child = NodeAt(node).Rect.Find(path) as RectTransform;
            if (child == null) throw new ArgumentException("No child " + path);
            var existing = _nodes.FindIndex(n => n.Rect == child);
            return existing >= 0 ? existing : Add(child);
        }

        // Initial values; channel 0 is the absolute base, 1-7 add (position, rotation) or multiply (scale, alpha).
        public void SetPosition(int node, int channel, float x, float y) => NodeAt(node).SetPosition(Channel(channel), new Vector2(x, y));
        public void SetRotation(int node, int channel, float z) => NodeAt(node).SetRotation(Channel(channel), z);
        public void SetScale(int node, int channel, float x, float y) => NodeAt(node).SetScale(Channel(channel), new Vector2(x, y));
        public void SetSize(int node, float width, float height) => NodeAt(node).Rect.sizeDelta = new Vector2(width, height);
        public void SetPivot(int node, float x, float y) => NodeAt(node).Rect.pivot = new Vector2(x, y);
        public void SetAlpha(int node, int channel, float alpha) => NodeAt(node).SetAlpha(Channel(channel), alpha);
        public void SetColor(int node, float r, float g, float b, float a) => NodeAt(node).SetColor(new Color(r, g, b, a));
        public void SetVisible(int node, bool visible) => NodeAt(node).SetVisible(visible);
        public void SetSprite(int node, string sprite) => NodeAt(node).SetSprite(Resource<Sprite>(sprite));

        public void SetPreserveAspect(int node, bool preserve)
        {
            var image = NodeAt(node).Image;
            if (image == null) throw new ArgumentException("Node is not an image.");
            image.preserveAspect = preserve;
        }

        public void SetFill(int node, float fill) => Glass(node).Fill = fill;

        /// <summary>Offsets the playback time seen by a glass's bubbles and liquid motion.</summary>
        public void SetClockOffset(int node, float seconds)
        {
            var glass = Glass(node);
            var index = _clocks.FindIndex(c => c.Graphic == glass);
            _clocks[index] = (glass, seconds);
        }

        // ---- Timeline. Every tween has explicit start and end values. ----

        public int Move(int node, int channel, float at, float duration, float x0, float y0, float x1, float y1, string ease)
        {
            var target = NodeAt(node);
            var from = new Vector2(x0, y0);
            var to = new Vector2(x1, y1);
            Channel(channel);
            return AddTween(at, duration, ease, t => target.SetPosition(channel, Vector2.LerpUnclamped(from, to, t)));
        }

        public int Rotate(int node, int channel, float at, float duration, float from, float to, string ease)
        {
            var target = NodeAt(node);
            Channel(channel);
            return AddTween(at, duration, ease, t => target.SetRotation(channel, Mathf.LerpUnclamped(from, to, t)));
        }

        public int Scale(int node, int channel, float at, float duration, float x0, float y0, float x1, float y1, string ease)
        {
            var target = NodeAt(node);
            var from = new Vector2(x0, y0);
            var to = new Vector2(x1, y1);
            Channel(channel);
            return AddTween(at, duration, ease, t => target.SetScale(channel, Vector2.LerpUnclamped(from, to, t)));
        }

        public int Fade(int node, int channel, float at, float duration, float from, float to, string ease)
        {
            var target = NodeAt(node);
            Channel(channel);
            return AddTween(at, duration, ease, t => target.SetAlpha(channel, Mathf.LerpUnclamped(from, to, t)));
        }

        public int Resize(int node, float at, float duration, float w0, float h0, float w1, float h1, string ease)
        {
            var rect = NodeAt(node).Rect;
            var from = new Vector2(w0, h0);
            var to = new Vector2(w1, h1);
            return AddTween(at, duration, ease, t => rect.sizeDelta = Vector2.LerpUnclamped(from, to, t));
        }

        public int Fill(int node, float at, float duration, float from, float to, string ease)
        {
            var glass = Glass(node);
            return AddTween(at, duration, ease, t => glass.Fill = Mathf.LerpUnclamped(from, to, t));
        }

        public void Loop(int tween, int count, bool yoyo)
        {
            if (tween < 0 || tween >= _tweens.Count) throw new ArgumentException("Unknown tween.");
            if (count < 1 || count > 64) throw new ArgumentException("Loop count must be between 1 and 64.");
            var spec = _tweens[tween];
            spec.Loops = count;
            spec.LoopType = yoyo ? LoopType.Yoyo : LoopType.Restart;
            CheckEnd(spec.At + spec.Duration * count);
        }

        public void Show(int node, float at, bool visible)
        {
            CheckEnd(at);
            _steps.Add(new Step { Node = NodeAt(node), At = at, Visible = visible });
        }

        public void Swap(int node, float at, string sprite)
        {
            CheckEnd(at);
            _steps.Add(new Step { Node = NodeAt(node), At = at, Sprite = Resource<Sprite>(sprite), IsSprite = true });
        }

        public void Sound(float at, string clip, float volume)
        {
            CheckEnd(at);
            _sounds.Add((at, Resource<AudioClip>(clip), Mathf.Clamp01(volume)));
        }

        /// <summary>A stage callback into Lua during forward playback; not called while a preview is positioned.</summary>
        public void Callback(float at, LuaFunction function)
        {
            CheckEnd(at);
            if (function == null) throw new ArgumentException("Callback requires a function.");
            _callbacks.Add((at, function));
        }

        // ---- Host side ----

        internal int NodeCount => _nodes.Count;

        internal Sequence Finish(Func<bool> muted, Action<AudioClip, float> play, Action<LuaFunction> call)
        {
            if (_finished) throw new InvalidOperationException("Gift playback already built.");
            _finished = true;
            _muted = muted;
            var sequence = DOTween.Sequence().SetUpdate(UpdateType.Normal, true).SetAutoKill(false).Pause();
            foreach (var spec in _tweens)
            {
                var apply = spec.Apply;
                var tween = DOVirtual.Float(0f, 1f, spec.Duration, value => apply(value)).SetEase(spec.Ease);
                if (spec.Loops > 1) tween.SetLoops(spec.Loops, spec.LoopType);
                sequence.Insert(spec.At, tween);
            }
            // Steps replay correctly in both directions: each one knows the state before it.
            foreach (var group in _steps.GroupBy(step => (step.Node, step.IsSprite)))
            {
                var visible = group.Key.Node.Visible;
                var sprite = group.Key.Node.Sprite;
                foreach (var step in group.OrderBy(step => step.At))
                {
                    var node = step.Node;
                    if (step.IsSprite)
                    {
                        Sprite before = sprite, after = step.Sprite;
                        sequence.Insert(step.At, DOVirtual.Float(0f, 1f, StepDuration, v => node.SetSprite(v >= 1f ? after : before)));
                        sprite = after;
                    }
                    else
                    {
                        bool before = visible, after = step.Visible;
                        sequence.Insert(step.At, DOVirtual.Float(0f, 1f, StepDuration, v => node.SetVisible(v >= 1f ? after : before)));
                        visible = after;
                    }
                }
            }
            foreach (var (at, clip, volume) in _sounds)
                sequence.InsertCallback(at, () => { if (!muted()) play(clip, volume); });
            foreach (var (at, function) in _callbacks)
                sequence.InsertCallback(at, () => { if (!muted()) call(function); });
            _timeline = sequence;
            sequence.OnUpdate(() => UpdateClocks(sequence.Elapsed()));
            return sequence;
        }

        internal void UpdateClocks(float elapsed)
        {
            UpdateConcurrentScales();
            foreach (var (graphic, offset) in _clocks)
                if (graphic != null) graphic.Clock = elapsed + offset;
        }

        internal void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            EndAudio();
            RunCleanup(_stopCallbacks);
            ConcurrentBindings.RemoveAll(binding => binding.Host == this);
            _timeline = null;
            AudioEnabled = null;
            RetainAudioResources = null;
            _muted = null;
            foreach (var (_, function) in _callbacks) function.Dispose();
            _callbacks.Clear();
        }

        private const float StepDuration = 0.0001f;

        private int AddTween(float at, float duration, string ease, Action<float> apply)
        {
            if (_tweens.Count >= MaxTweens) throw new InvalidOperationException("Too many gift tweens.");
            if (duration < 0f || float.IsNaN(duration)) throw new ArgumentException("Invalid duration.");
            CheckEnd(at + duration);
            _tweens.Add(new TweenSpec { At = at, Duration = duration, Ease = ParseEase(ease), Apply = apply });
            return _tweens.Count - 1;
        }

        private static readonly Dictionary<string, Ease> Eases = new Dictionary<string, Ease>(StringComparer.Ordinal);

        private static Ease ParseEase(string name)
        {
            if (string.IsNullOrEmpty(name)) return Ease.Linear;
            if (Eases.TryGetValue(name, out var ease)) return ease;
            if (!Enum.TryParse(name, false, out ease) || ease == Ease.Unset || ease == Ease.INTERNAL_Zero || ease == Ease.INTERNAL_Custom)
                throw new ArgumentException("Unknown ease: " + name);
            Eases[name] = ease;
            return ease;
        }

        private static void CheckEnd(float time)
        {
            if (time < 0f || float.IsNaN(time) || time > MaxDuration) throw new ArgumentException("Gift time must be within 0-" + MaxDuration + " seconds.");
        }

        private static int Channel(int channel)
        {
            if (channel < 0 || channel >= GiftNode.Channels) throw new ArgumentException("Channel must be 0-" + (GiftNode.Channels - 1) + ".");
            return channel;
        }

        private GiftNode NodeAt(int node)
        {
            if (node < 0 || node >= _nodes.Count) throw new ArgumentException("Unknown node " + node);
            return _nodes[node];
        }

        private ChampagneGlassGraphic Glass(int node)
        {
            var glass = NodeAt(node).Rect.GetComponent<ChampagneGlassGraphic>();
            if (glass == null) throw new ArgumentException("Node is not a glass.");
            return glass;
        }

        private RectTransform CreateRect(int parent, string name, float width, float height)
        {
            if (_nodes.Count >= MaxNodes) throw new InvalidOperationException("Too many gift nodes.");
            var rect = (RectTransform)new GameObject(string.IsNullOrEmpty(name) ? "Node" : name, typeof(RectTransform)).transform;
            rect.SetParent(NodeAt(parent).Rect, false);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private int Add(RectTransform rect)
        {
            if (_nodes.Count >= MaxNodes) throw new InvalidOperationException("Too many gift nodes.");
            _nodes.Add(new GiftNode(rect));
            return _nodes.Count - 1;
        }

        private static Graphic Accent(RectTransform rect, RottenEggAccent.Shape shape)
        {
            var accent = rect.gameObject.AddComponent<RottenEggAccent>();
            accent.Kind = shape;
            return accent;
        }

        private static Graphic Gunfire(RectTransform rect, GunfireAccent.Shape shape)
        {
            var accent = rect.gameObject.AddComponent<GunfireAccent>();
            accent.Kind = shape;
            return accent;
        }

        private T Resource<T>(string name) where T : Object
        {
            if (_resource(name) is T asset) return asset;
            throw new ArgumentException("Gift resource " + name + " is not a " + typeof(T).Name + ".");
        }

        private void Point(RectTransform card, float offsetX, float offsetY, out float x, out float y)
        {
            if (card == null) throw new InvalidOperationException("This gift has no such card.");
            var point = CardPoint(card, new Vector2(offsetX, offsetY));
            x = point.x;
            y = point.y;
        }

        private void Size(RectTransform card, out float width, out float height)
        {
            if (card == null) throw new InvalidOperationException("This gift has no such card.");
            width = Vector2.Distance(CardPoint(card, Vector2.left * 0.5f), CardPoint(card, Vector2.right * 0.5f));
            height = Vector2.Distance(CardPoint(card, Vector2.down * 0.5f), CardPoint(card, Vector2.up * 0.5f));
        }

        // A point at a fraction of the card's size from its center, in the effect root's local space.
        private Vector2 CardPoint(RectTransform card, Vector2 offset)
        {
            var canvas = card.GetComponentInParent<Canvas>().rootCanvas;
            var rootCanvas = _root.GetComponentInParent<Canvas>().rootCanvas;
            var world = card.TransformPoint(card.rect.center + Vector2.Scale(card.rect.size, offset));
            var screen = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen,
                rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera, out var local);
            return local;
        }
    }
}
