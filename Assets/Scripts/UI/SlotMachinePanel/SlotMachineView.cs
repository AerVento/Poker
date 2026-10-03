using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // Presentation only: no wallet access, purchases or reward grants.
    public sealed class SlotMachineView : MonoBehaviour
    {
        [SerializeField] private Button _lever;
        [SerializeField] private RectTransform _leverArm;
        [SerializeField] private RectTransform _leverShaft;
        [SerializeField] private RectTransform _leverKnob;
        [SerializeField] private SlotReelView[] _reels;
        [SerializeField] private TextMeshProUGUI _result;
        [SerializeField] private TextMeshProUGUI _leverLabel;
        [SerializeField] private Image[] _lamps;
        [SerializeField] private Image[] _prizeOutlines;
        [SerializeField] private SlotCelebrationGraphic _celebrationEffect;
        [SerializeField] private AudioSource _sound;
        [SerializeField] private AudioClip _backgroundMusic;
        [SerializeField] private AudioClip _pullSound;
        [SerializeField] private AudioClip _returnSound;
        [SerializeField] private AudioClip _reelStopSound;
        [SerializeField] private AudioClip _spinMusic;
        [SerializeField] private AudioClip[] _resultSounds;
        private CancellationTokenSource _lifetime;
        private bool _busy;
        private bool _canRequest;
        public event Action SpinRequested;
        public event Action SpinFinished;
        private int _winningTier = -1;

        private void OnEnable()
        {
            _lifetime = new CancellationTokenSource();
            _lever.onClick.AddListener(RequestSpin);
            AudioPreferences.Changed += ApplyVolume;
            ApplyVolume();
            ResetPresentation();
            GameAudioManager.EnsureClient()?.SetMusicOverride(this, _backgroundMusic);
        }

        private void OnDisable()
        {
            _lever.onClick.RemoveListener(RequestSpin);
            AudioPreferences.Changed -= ApplyVolume;
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            _sound.Stop();
            GameAudioManager.Existing?.ClearMusicOverride(this);
            ResetPresentation();
        }

        private void ApplyVolume()
        {
            _sound.volume = AudioPreferences.SoundVolume;
            _sound.mute = AudioPreferences.SoundMuted;
            if (AudioPreferences.SoundVolume <= 0f) _sound.Stop();
        }

        private void ResetPresentation()
        {
            _busy = false;
            _celebrationEffect.Clear();
            _winningTier = -1;
            _lever.interactable = _canRequest;
            SetLeverPose(0f);
            _leverLabel.text = "抽奖";
            _result.text = "好运将至";
            _result.color = new Color(1f, 0.88f, 0.6f);
            _result.rectTransform.localScale = Vector3.one;
            for (int i = 0; i < _reels.Length; i++) _reels[i].SetTravel(0f, i);
            foreach (var outline in _prizeOutlines) outline.color = new Color(1f, 1f, 1f, 0.2f);
        }

        public void SetAvailability(bool available)
        {
            _canRequest = available;
            _lever.interactable = !_busy && available;
        }

        private void RequestSpin()
        {
            if (!_busy && _canRequest) SpinRequested?.Invoke();
        }

        public void Present(SlotPrizeTier tier, int count = 1)
        {
            if (!isActiveAndEnabled || _busy || tier < SlotPrizeTier.None || tier > SlotPrizeTier.Grand)
                return;
            _busy = true;
            _lever.interactable = false;
            SpinAsync(tier, count, _lifetime.Token).Forget();
        }

        private async UniTask SpinAsync(SlotPrizeTier tier, int count, CancellationToken token)
        {
            bool resultPresented = false;
            try
            {
                _celebrationEffect.Clear();
                _winningTier = -1;
                _sound.Stop();
                foreach (var outline in _prizeOutlines) outline.color = new Color(1f, 1f, 1f, 0.2f);
                _result.text = count == 10 ? "10 连抽中…" : "转动中…";
                _result.color = Color.white;
                _leverLabel.text = "转动中";
                PlaySound(_pullSound);
                await MoveLever(0f, 1f, 0.24f, token);
                GameAudioManager.Existing?.SetMusicOverride(this, _spinMusic);
                var stopped = new bool[3];
                var steps = new int[3];
                var symbols = CreateReelSymbols(tier);
                for (int i = 0; i < 3; i++)
                {
                    int symbol = symbols[i];
                    steps[i] = 30 + i * 6 + (symbol - i + 3) % 3;
                }
                float started = Time.unscaledTime;
                int stoppedCount = 0;
                while (stoppedCount < 3)
                {
                    token.ThrowIfCancellationRequested();
                    float elapsed = Time.unscaledTime - started;
                    for (int i = 0; i < 3; i++)
                    {
                        if (stopped[i]) continue;
                        float duration = 3f + i * 0.6f;
                        if (elapsed >= duration)
                        {
                            // Lock the symbol and trigger its stop cue in the same frame.
                            _reels[i].SetTravel(steps[i], i);
                            stopped[i] = true;
                            stoppedCount++;
                            PlaySound(_reelStopSound);
                            continue;
                        }
                        const float brakingTime = 0.45f;
                        float cruiseTime = duration - brakingTime;
                        float speed = steps[i] / (cruiseTime + brakingTime * 0.5f);
                        float braking = Mathf.Max(0f, elapsed - cruiseTime);
                        float travel = speed * (Mathf.Min(elapsed, cruiseTime) + braking -
                            braking * braking / (2f * brakingTime));
                        _reels[i].SetTravel(travel, i);
                    }
                    if (stoppedCount < 3) await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                _winningTier = (int)tier - 1;
                _result.text = tier == SlotPrizeTier.None ? "未中奖" : tier == SlotPrizeTier.Small ? "小奖！" : tier == SlotPrizeTier.Medium ? "中等奖！" : "大奖！";
                if (count == 10) _result.text = tier == SlotPrizeTier.None ? "10 连抽 · 未中奖" : "10 连抽 · 最佳：" + _result.text;
                _result.color = tier == SlotPrizeTier.None ? new Color(0.8f, 0.74f, 0.87f) : new Color(1f, 0.88f, 0.4f);
                GameAudioManager.Existing?.PlayMusicOnce(this, _resultSounds[(int)tier], _backgroundMusic);
                if (tier != SlotPrizeTier.None)
                {
                    _celebrationEffect.Play(tier);
                    _prizeOutlines[_winningTier].color = Color.white;
                }
                resultPresented = true;
                PlaySound(_returnSound);
                await MoveLever(1f, 0f, 0.24f, token);
                _leverLabel.text = "抽奖";
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    if (!resultPresented)
                        GameAudioManager.Existing?.SetMusicOverride(this, _backgroundMusic);
                    SetLeverPose(0f);
                    _busy = false;
                    _lever.interactable = _canRequest;
                    if (resultPresented) SpinFinished?.Invoke();
                }
            }
        }

        private static int[] CreateReelSymbols(SlotPrizeTier tier)
        {
            if (tier != SlotPrizeTier.None)
                return new[] { (int)tier - 1, (int)tier - 1, (int)tier - 1 };

            // The 24 non-winning combinations are equally likely; skip 000, 111 and 222.
            int combination = UnityEngine.Random.Range(0, 24) + 1;
            if (combination >= 13) combination++;
            return new[] { combination / 9, combination / 3 % 3, combination % 3 };
        }

        private async UniTask MoveLever(float from, float to, float duration, CancellationToken token)
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < duration)
            {
                token.ThrowIfCancellationRequested();
                float t = Mathf.Clamp01((Time.unscaledTime - start) / duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                SetLeverPose(Mathf.LerpUnclamped(from, to, ease));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetLeverPose(to);
        }

        private void SetLeverPose(float pull)
        {
            // Front projection of a lever rotating toward the player about a horizontal axle.
            // The knob stays round, grows slightly toward the viewer, and never moves sideways.
            float angle = Mathf.Lerp(0f, 140f, pull) * Mathf.Deg2Rad;
            float height = Mathf.Cos(angle) * 210f;
            float depth = Mathf.Sin(angle);
            _leverArm.localRotation = Quaternion.identity;
            _leverKnob.anchoredPosition = new Vector2(0f, height);
            _leverKnob.localScale = Vector3.one * (1f + 0.18f * depth);
            _leverShaft.anchoredPosition = new Vector2(0f, height * 0.5f);
            _leverShaft.sizeDelta = new Vector2(24f + 3f * depth, Mathf.Max(1f, Mathf.Abs(height)));
            _leverShaft.localScale = new Vector3(1f, height < 0f ? -1f : 1f, 1f);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && AudioPreferences.SoundVolume > 0f) _sound.PlayOneShot(clip);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            bool celebrating = _celebrationEffect.IsPlaying;
            for (int i = 0; i < _lamps.Length; i++)
            {
                float speed = _busy ? 9f : celebrating ? (_winningTier == 2 ? 7f : 4f) : 2f;
                float wave = 0.5f + 0.5f * Mathf.Sin(now * speed - i * 0.65f);
                Color glow = new Color(1f, 0.86f, 0.46f, 0.8f);
                if (celebrating && _winningTier == 1 && i % 2 == 0)
                    glow = new Color(0.25f, 0.8f, 1f, 0.85f);
                else if (celebrating && _winningTier == 2 && i % 3 == 0)
                    glow = new Color(1f, 0.3f, 0.65f, 0.9f);
                _lamps[i].color = Color.Lerp(new Color(0.65f, 0.31f, 0.08f, 0.15f), glow, wave);
            }
            float pulse = celebrating ? Mathf.Sin(_celebrationEffect.Elapsed * 5f) : 0f;
            float emphasis = _winningTier == 2 ? 0.13f : _winningTier == 1 ? 0.07f : 0.025f;
            _result.rectTransform.localScale = Vector3.one * (1f + emphasis * Mathf.Max(0f, pulse));
            if (celebrating && _winningTier >= 0)
            {
                Color highlight = _winningTier == 1 ? new Color(0.3f, 0.85f, 1f) : new Color(1f, 0.85f, 0.35f);
                _prizeOutlines[_winningTier].color = Color.Lerp(highlight, Color.white, 0.5f + pulse * 0.5f);
            }
        }
    }
}
