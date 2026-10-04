using System;
using DG.Tweening;
using Game.SO;
using UnityEngine;
using XLua;

namespace Game.UI.Gifts
{
    /// <summary>
    /// Plays a gift defined by Lua. The script builds one root Sequence; C# owns it, the nodes and the audio, and
    /// cleans everything up on stop, disable, a missing card or the end of the timeline.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class LuaGiftEffect : GiftEffect
    {
        private const int PreviewSeed = 20241;
        private GiftEffectSO _definition;
        private GiftScript _script;
        private RectTransform _layer;
        private GiftScriptHost _host;
        private Sequence _sequence;
        private RectTransform _sender;
        private RectTransform _target;
        private bool _preview;
        private bool _muted;

        public override float Duration => _script?.Duration ?? 0f;
        public override bool SupportsTimeline => true;
        public bool IsPlaying => _sequence != null;
        public int NodeCount => _host?.NodeCount ?? 0;

        internal static LuaGiftEffect Create(GiftEffectSO definition, GiftScript script, Transform parent)
        {
            var root = new GameObject(definition.name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var effect = root.AddComponent<LuaGiftEffect>();
            effect._definition = definition;
            effect._script = script;
            return effect;
        }

        public override void Play(RectTransform target) => Play(null, target);

        public override void Play(RectTransform sender, RectTransform target)
        {
            if (!Begin(sender, target, false)) return;
            _sequence.Play();
        }

        public override void PreviewAt(RectTransform target, float seconds) => PreviewAt(null, target, seconds);

        // Positions a fixed-seed build; sounds and stage callbacks stay silent while scrubbing.
        public override void PreviewAt(RectTransform sender, RectTransform target, float seconds)
        {
            if (_sequence == null || !_preview || _sender != sender || _target != target)
                if (!Begin(sender, target, true)) return;
            var time = Mathf.Clamp(seconds, 0f, Duration);
            _sequence.Goto(time, false);
            _host.UpdateClocks(time);
        }

        public override void Stop()
        {
            StopAudio();
            _sequence?.Kill();
            _sequence = null;
            _host?.Dispose();
            _host = null;
            if (_layer != null) Destroy(_layer.gameObject);
            _layer = null;
            _sender = _target = null;
            _preview = false;
        }

        private bool Begin(RectTransform sender, RectTransform target, bool preview)
        {
            Stop();
            if (_script == null || !isActiveAndEnabled || !Available(target) || sender != null && !Available(sender)) return false;
            _layer = CreateLayer();
            _sender = sender;
            _target = target;
            _preview = preview;
            _muted = preview;
            var seed = preview ? PreviewSeed : Environment.TickCount ^ GetInstanceID();
            BeginAudio();
            _host = new GiftScriptHost((RectTransform)transform, _layer, sender, target, _definition.FindResource, seed);
            _host.AudioEnabled = () => SoundEnabled && isActiveAndEnabled;
            _host.RetainAudioResources = RetainAudioResources;
            try
            {
                GiftScriptRuntime.Build(_script, _host);
                _sequence = _host.Finish(() => _muted, PlayAudio, RunCallback);
                var built = _sequence.Duration();
                if (Mathf.Abs(built - _script.Duration) > 0.05f)
                    throw new GiftScriptException("Declared duration " + _script.Duration.ToString("0.00") +
                        "s does not match the timeline " + built.ToString("0.00") + "s.");
            }
            catch (Exception exception)
            {
                _definition.ReportError(exception.Message);
                Stop();
                return false;
            }
            _host.UpdateClocks(0f);
            return true;
        }

        protected override void StopMusic() => _host?.EndAudio();

        private RectTransform CreateLayer()
        {
            var layer = (RectTransform)new GameObject("Visual", typeof(RectTransform)).transform;
            layer.SetParent(transform, false);
            layer.sizeDelta = Vector2.zero;
            var canvas = layer.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            var group = layer.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            return layer;
        }

        private void RunCallback(LuaFunction function)
        {
            try { GiftScriptRuntime.Call(function); }
            catch (Exception exception)
            {
                _definition.ReportError(exception.Message);
                Stop();
            }
        }

        private static bool Available(RectTransform card) => card != null && card.gameObject.activeInHierarchy;

        private void LateUpdate()
        {
            if (_sequence == null) return;
            // Cards are measured once at the start; a hidden or departed card ends the effect.
            if (!Available(_target) || _sender != null && !Available(_sender)) { Stop(); return; }
            GiftScriptRuntime.Tick();
            if (_preview) return;
            _host.UpdateClocks(_sequence.Elapsed());
            if (_sequence.IsComplete()) Stop();
        }

        private void OnDisable() => Stop();

        protected override void OnDestroy()
        {
            Stop();
            base.OnDestroy();
        }
    }
}
