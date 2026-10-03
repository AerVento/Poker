using Game.Network;
using UnityEngine;

namespace Game
{
    public sealed class GameAudioManager : MonoBehaviour
    {
        public const string LibraryResourcePath = "SO/GameAudioLibrary";
        private static GameAudioManager _instance;

        private readonly PokerAudioState _state = new PokerAudioState();
        private readonly PokerOperationAudioState _operations = new PokerOperationAudioState();
        private GameAudioLibrary _library;
        private AudioSource _musicSource;
        private AudioSource _soundSource;
        private MusicCue _playingMusic;
        private MusicCue _requestedMusic = MusicCue.Lobby;
        private UnityEngine.Object _musicOverrideOwner;
        private AudioClip _musicOverrideClip;
        private AudioClip _musicOnceClip;
        private double _musicOnceStartedAt = -1d;
        private bool _musicNeedsRestart;
        private const float MusicFadeOutSeconds = 0.2f;
        private const float MusicFadeInSeconds = 0.4f;
        private float _musicEnvelope;
        private float _duckUntil;
        private float _duckEnvelope = 1f;
        private bool _preferencesDirty;
        private float _saveAt;

        public static GameAudioManager Existing => _instance;
        internal event System.Action<SoundCue> SoundRequested;
        public float MusicVolume => AudioPreferences.MusicVolume;
        public float SoundVolume => AudioPreferences.SoundVolume;

        public static GameAudioManager EnsureClient()
        {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_WEBGL
            if (!Application.isPlaying || NetworkConfigurationLoader.IsServerProcess)
                return null;
            if (_instance == null)
                new GameObject(nameof(GameAudioManager)).AddComponent<GameAudioManager>();
            return _instance;
#else
            return null;
#endif
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _library = Resources.Load<GameAudioLibrary>(LibraryResourcePath);
            if (_library == null)
                Debug.LogError("Poker audio library is missing: " + LibraryResourcePath);
            _musicSource = CreateSource("Music", true);
            _soundSource = CreateSource("SoundEffects", false);
            AudioPreferences.Changed += ApplyPreferences;
            ApplyPreferences();
        }

        private AudioSource CreateSource(string objectName, bool loop)
        {
            var sourceObject = new GameObject(objectName);
            sourceObject.transform.SetParent(transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = loop ? 128 : 32;
            return source;
        }

        public void EnterLobby()
        {
            if (_state.Music != MusicCue.Lobby)
                _soundSource.Stop();
            _state.EnterLobby();
            _operations.Reset();
            _duckUntil = 0f;
            _requestedMusic = MusicCue.Lobby;
        }

        public void ObserveGame(ClientGameInfo info)
        {
            foreach (var cue in _operations.Observe(info))
                PlaySound(cue);
            bool notify = _state.Observe(info);
            _requestedMusic = _state.Music;
            if (notify)
                PlaySound(SoundCue.YourTurn);
        }

        public void PlayOperation(PlayerOperationType type, ClientGameInfo info, uint number)
        {
            _operations.RecordLocal(info);
            PlaySound(PokerAudioState.GetOperation(type, info, number));
        }

        public void ShowResult(int roomId, string handId, string playerName, RoundResult result)
        {
            var cue = _state.GetResult(roomId, handId, playerName, result);
            if (cue.HasValue)
                PlaySound(cue.Value);
        }

        public void PlaySound(SoundCue cue)
        {
            SoundRequested?.Invoke(cue);
            var clip = _library != null ? _library.GetSound(cue) : null;
            if (clip == null || SoundVolume <= 0f)
                return;
            // One AudioSource supports overlapping one-shots without interrupting them.
            _soundSource.PlayOneShot(clip);
            if (cue == SoundCue.Win || cue == SoundCue.Lose)
                _duckUntil = Time.unscaledTime + clip.length;
        }

        // An owned temporary track shares the single music channel and its mute settings.
        public void SetMusicOverride(UnityEngine.Object owner, AudioClip clip)
        {
            if (owner == null || clip == null) return;
            _musicOverrideOwner = owner;
            _musicOverrideClip = clip;
            ClearMusicOnce();
        }

        // Exclusive one-shot music; the owning page supplies the loop to resume afterwards.
        public void PlayMusicOnce(UnityEngine.Object owner, AudioClip clip, AudioClip resumeClip)
        {
            if (owner == null || resumeClip == null) return;
            SetMusicOverride(owner, resumeClip);
            _musicOnceClip = clip;
        }

        private void ClearMusicOnce()
        {
            _musicOnceClip = null;
            _musicOnceStartedAt = -1d;
        }

        public void ClearMusicOverride(UnityEngine.Object owner)
        {
            if (_musicOverrideOwner != owner) return;
            _musicOverrideOwner = null;
            _musicOverrideClip = null;
            ClearMusicOnce();
        }

        public void SetMusicVolume(float value) => AudioPreferences.SetMusicVolume(value);
        public void SetSoundVolume(float value) => AudioPreferences.SetSoundVolume(value);
        public void SetMusicMuted(bool muted) => AudioPreferences.SetMusicMuted(muted);
        public void SetSoundMuted(bool muted) => AudioPreferences.SetSoundMuted(muted);

        private void ApplyPreferences()
        {
            _musicSource.mute = AudioPreferences.MusicMuted;
            _soundSource.mute = AudioPreferences.SoundMuted;
            _musicSource.volume = MusicVolume * _musicEnvelope * _duckEnvelope;
            _soundSource.volume = SoundVolume;
            if (MusicVolume <= 0f)
            {
                _musicSource.Stop();
                _musicNeedsRestart = true;
                _playingMusic = MusicCue.None;
                _musicEnvelope = 0f;
            }
            if (SoundVolume <= 0f)
            {
                _soundSource.Stop();
                _duckUntil = 0f;
            }
            ScheduleSave();
        }

        private void ScheduleSave()
        {
            _preferencesDirty = true;
            _saveAt = Time.unscaledTime + 0.5f;
        }

        public void SaveVolumes()
        {
            if (!_preferencesDirty)
                return;
            AudioPreferences.Save();
            _preferencesDirty = false;
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            UpdateMusic(delta);

            _duckEnvelope = Mathf.MoveTowards(_duckEnvelope,
                Time.unscaledTime < _duckUntil ? 0.45f : 1f, delta * 4f);
            _musicSource.volume = MusicVolume * _musicEnvelope * _duckEnvelope;
            if (_preferencesDirty && Time.unscaledTime >= _saveAt)
                SaveVolumes();
        }

        private void UpdateMusic(float delta)
        {
            // Destroying the owner cancels its pending return as well as its current track.
            if (_musicOverrideOwner == null) ClearMusicOnce();
            double now = AudioSettings.dspTime;
            if (_musicOnceClip != null && _musicOnceStartedAt >= 0d &&
                now - _musicOnceStartedAt >= _musicOnceClip.length &&
                (MusicVolume <= 0f || !_musicSource.isPlaying))
            {
                ClearMusicOnce();
            }

            bool once = _musicOverrideOwner != null && _musicOnceClip != null;
            var desiredClip = once ? _musicOnceClip : _musicOverrideOwner != null && _musicOverrideClip != null
                ? _musicOverrideClip : _library != null ? _library.GetMusic(_requestedMusic) : null;
            bool loop = !once;
            if (MusicVolume <= 0f)
            {
                if (_musicSource.isPlaying) _musicSource.Stop();
                _musicNeedsRestart = true;
                _playingMusic = MusicCue.None;
                _musicEnvelope = 0f;
                // Muted celebrations elapse silently instead of replaying late on unmute.
                if (once && _musicOnceStartedAt < 0d) _musicOnceStartedAt = now;
                return;
            }

            if (_musicSource.clip != desiredClip || _musicSource.loop != loop ||
                _musicNeedsRestart || (once && _musicOnceStartedAt < 0d))
            {
                _musicEnvelope = Mathf.MoveTowards(_musicEnvelope, 0f, delta / MusicFadeOutSeconds);
                if (_musicEnvelope <= 0f)
                {
                    // A single source guarantees the outgoing and incoming music never overlap.
                    _musicSource.Stop();
                    _musicSource.volume = 0f;
                    _musicSource.clip = desiredClip;
                    _musicSource.loop = loop;
                    _playingMusic = _requestedMusic;
                    _musicNeedsRestart = false;
                    if (desiredClip != null)
                    {
                        if (once)
                        {
                            if (_musicOnceStartedAt < 0d) _musicOnceStartedAt = now;
                            _musicSource.time = Mathf.Clamp((float)(now - _musicOnceStartedAt),
                                0f, Mathf.Max(0f, desiredClip.length - 0.001f));
                        }
                        _musicSource.Play();
                    }
                }
            }
            else
            {
                float target = once && _musicOnceStartedAt >= 0d
                    ? Mathf.Clamp01((float)(_musicOnceClip.length - (now - _musicOnceStartedAt)) / MusicFadeOutSeconds)
                    : 1f;
                float duration = target < _musicEnvelope ? MusicFadeOutSeconds : MusicFadeInSeconds;
                _musicEnvelope = Mathf.MoveTowards(_musicEnvelope, target, delta / duration);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveVolumes();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                SaveVolumes();
        }

        private void OnApplicationQuit() => SaveVolumes();

        private void OnDestroy()
        {
            if (_instance != this)
                return;
            AudioPreferences.Changed -= ApplyPreferences;
            SaveVolumes();
            _instance = null;
        }
    }
}
