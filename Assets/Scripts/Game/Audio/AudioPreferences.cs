using UnityEngine;

namespace Game
{
    public static class AudioPreferences
    {
        public const string MusicVolumeKey = "Poker.Audio.MusicVolume";
        public const string SoundVolumeKey = "Poker.Audio.SoundVolume";
        public const float DefaultMusicVolume = 0.35f;
        public const float DefaultSoundVolume = 0.7f;

        public static float MusicVolume => Sanitize(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
        public static float SoundVolume => Sanitize(PlayerPrefs.GetFloat(SoundVolumeKey, DefaultSoundVolume));

        public static float Sanitize(float value) => float.IsNaN(value) || float.IsInfinity(value)
            ? 0f : Mathf.Clamp01(value);

        public static void SetMusicVolume(float value) => PlayerPrefs.SetFloat(MusicVolumeKey, Sanitize(value));
        public static void SetSoundVolume(float value) => PlayerPrefs.SetFloat(SoundVolumeKey, Sanitize(value));
        public static void Save() => PlayerPrefs.Save();
    }
}
