using System;
using System.Collections.Generic;
using Game.Network;
using UnityEngine;

namespace Game.UI.Gifts
{
    /// <summary>Generic Lua-owned audio handle. Sharing and music selection are decided by Lua.</summary>
    public sealed class GiftMusicPlayback : MonoBehaviour
    {
        private static readonly Dictionary<int, GiftMusicPlayback> Playing = new Dictionary<int, GiftMusicPlayback>();
        private static int _nextHandle;
        private readonly HashSet<GiftScriptHost> _owners = new HashSet<GiftScriptHost>();
        private int _handle;
        private AudioSource _source;
        private IDisposable _resources;
        private GameAudioManager _audioManager;
        private float _gain, _from, _to, _elapsed, _duration;
        private bool _stopAfterFade;
        private bool _ended;

        internal static GiftMusicPlayback Find(int handle) => Playing.TryGetValue(handle, out var playback) ? playback : null;

        internal static int Start(AudioClip clip, float volume, bool loop, GiftScriptHost owner, Func<IDisposable> retainResources)
        {
            if (clip == null || NetworkConfigurationLoader.IsServerProcess || Playing.Count >= 16) return 0;
            var resources = retainResources?.Invoke();
            var root = new GameObject(nameof(GiftMusicPlayback));
            var playback = root.AddComponent<GiftMusicPlayback>();
            playback._handle = ++_nextHandle;
            playback._resources = resources;
            playback._gain = playback._from = playback._to = volume;
            playback._source = root.AddComponent<AudioSource>();
            playback._source.playOnAwake = false;
            playback._source.spatialBlend = 0f;
            playback._source.priority = 128;
            playback._source.loop = loop;
            playback._source.clip = clip;
            playback.ApplyVolume();
            playback._owners.Add(owner);
            Playing.Add(playback._handle, playback);
            playback._audioManager = GameAudioManager.EnsureClient();
            playback._audioManager?.SetMusicSuppressed(playback, true);
            if (Application.isPlaying) playback._source.Play();
            return playback._handle;
        }

        internal void Retain(GiftScriptHost owner) => _owners.Add(owner);

        internal void Release(GiftScriptHost owner)
        {
            _owners.Remove(owner);
            // A short, explicit fade may outlive the visuals. Otherwise never leave an orphaned source
            // behind, including when a Lua cleanup callback throws or forgets to stop its handle.
            if (_owners.Count == 0 && !_stopAfterFade) Stop();
        }

        internal void Fade(float volume, float seconds, bool stop)
        {
            _from = _gain;
            _to = volume;
            _elapsed = 0f;
            _duration = seconds;
            _stopAfterFade = stop;
            if (seconds <= 0f) Advance(0f);
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        private void Advance(float delta)
        {
            if (_ended) return;
            _elapsed += delta;
            float fraction = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
            _gain = Mathf.Lerp(_from, _to, fraction);
            ApplyVolume();
            if (fraction >= 1f && _stopAfterFade || Application.isPlaying && !_source.loop && !_source.isPlaying) Stop();
        }

        private void ApplyVolume()
        {
            _source.mute = AudioPreferences.MusicMuted;
            _source.volume = AudioPreferences.MusicVolume * _gain;
        }

        private void Stop()
        {
            End();
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void End()
        {
            if (_ended) return;
            _ended = true;
            Playing.Remove(_handle);
            if (_source != null) { _source.Stop(); _source.clip = null; }
            _owners.Clear();
            _resources?.Dispose();
            _resources = null;
            if (_audioManager != null) _audioManager.SetMusicSuppressed(this, false);
            _audioManager = null;
        }

        private void OnDisable() => End();
        private void OnDestroy() => End();
    }
}
