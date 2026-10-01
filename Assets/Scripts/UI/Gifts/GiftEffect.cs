using UnityEngine;

namespace Game.UI.Gifts
{
    [RequireComponent(typeof(RectTransform))]
    public abstract class GiftEffect : MonoBehaviour
    {
        public abstract float Duration { get; }
        public abstract void Play(RectTransform target);
        public virtual void Play(RectTransform sender, RectTransform target) => Play(target);
        public virtual void Stop() { }
        public virtual void PreviewAt(RectTransform target, float seconds) { }
        public virtual void PreviewAt(RectTransform sender, RectTransform target, float seconds) => PreviewAt(target, seconds);
        public virtual bool SupportsTimeline => false;
        private bool _soundEnabled = true;
        public bool SoundEnabled
        {
            get => _soundEnabled;
            set
            {
                _soundEnabled = value;
                if (!value && _giftAudio != null) _giftAudio.Stop();
            }
        }

        private AudioSource _giftAudio;
        private System.IDisposable _resources;

        internal void RetainResources(System.IDisposable resources)
        {
            _resources?.Dispose();
            _resources = resources;
        }
        protected void BeginAudio()
        {
            StopAudio();
            AudioPreferences.Changed += ApplyAudioPreferences;
        }

        protected void PlayAudio(AudioClip clip, float volumeScale = 1f)
        {
            if (!SoundEnabled || AudioPreferences.SoundVolume <= 0f || clip == null) return;
            if (_giftAudio == null)
            {
                _giftAudio = gameObject.AddComponent<AudioSource>();
                _giftAudio.playOnAwake = false;
                _giftAudio.spatialBlend = 0f;
                _giftAudio.priority = 180;
            }
            _giftAudio.mute = AudioPreferences.SoundMuted;
            _giftAudio.volume = AudioPreferences.SoundVolume * 0.65f;
            _giftAudio.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        private void ApplyAudioPreferences()
        {
            if (_giftAudio == null) return;
            _giftAudio.mute = AudioPreferences.SoundMuted;
            _giftAudio.volume = AudioPreferences.SoundVolume * 0.65f;
            if (!SoundEnabled || AudioPreferences.SoundVolume <= 0f) _giftAudio.Stop();
        }

        protected virtual void OnDestroy()
        {
            StopAudio();
            _resources?.Dispose();
            _resources = null;
        }

        protected void StopAudio()
        {
            AudioPreferences.Changed -= ApplyAudioPreferences;
            if (_giftAudio != null) _giftAudio.Stop();
        }
    }
}
