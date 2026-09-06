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
        private float _musicEnvelope;
        private float _duckUntil;
        private float _duckEnvelope = 1f;
        private bool _preferencesDirty;
        private float _saveAt;

        public static GameAudioManager Existing => _instance;
        internal event System.Action<SoundCue> SoundRequested;
        public float MusicVolume { get; private set; }
        public float SoundVolume { get; private set; }
        public MusicCue CurrentMusic => _playingMusic;
        public MusicCue RequestedMusic => _requestedMusic;

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
            MusicVolume = AudioPreferences.MusicVolume;
            SoundVolume = AudioPreferences.SoundVolume;
            _soundSource.volume = SoundVolume;
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

        public void SetMusicVolume(float value)
        {
            MusicVolume = AudioPreferences.Sanitize(value);
            AudioPreferences.SetMusicVolume(MusicVolume);
            _musicSource.volume = MusicVolume * _musicEnvelope * _duckEnvelope;
            ScheduleSave();
        }

        public void SetSoundVolume(float value)
        {
            SoundVolume = AudioPreferences.Sanitize(value);
            AudioPreferences.SetSoundVolume(SoundVolume);
            _soundSource.volume = SoundVolume;
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
            if (_playingMusic != _requestedMusic)
            {
                _musicEnvelope = Mathf.MoveTowards(_musicEnvelope, 0f, delta / 0.2f);
                if (_musicEnvelope <= 0f)
                {
                    // Stop the previous track before assigning the next: never two BGMs.
                    _musicSource.Stop();
                    _playingMusic = _requestedMusic;
                    _musicSource.clip = _library != null ? _library.GetMusic(_playingMusic) : null;
                    if (_musicSource.clip != null)
                        _musicSource.Play();
                }
            }
            else
                _musicEnvelope = Mathf.MoveTowards(_musicEnvelope, 1f, delta / 0.4f);

            _duckEnvelope = Mathf.MoveTowards(_duckEnvelope,
                Time.unscaledTime < _duckUntil ? 0.45f : 1f, delta * 4f);
            _musicSource.volume = MusicVolume * _musicEnvelope * _duckEnvelope;
            if (_preferencesDirty && Time.unscaledTime >= _saveAt)
                SaveVolumes();
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
            SaveVolumes();
            _instance = null;
        }
    }
}
