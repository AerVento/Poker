using UnityEngine;

namespace Game
{
    public static class AudioPreferences
    {
        public const string MusicVolumeKey = "Poker.Audio.MusicVolume";
        public const string SoundVolumeKey = "Poker.Audio.SoundVolume";
        public const string MusicMutedKey = "Poker.Audio.MusicMuted";
        public const string SoundMutedKey = "Poker.Audio.SoundMuted";
        public const float DefaultMusicVolume = 0.35f;
        public const float DefaultSoundVolume = 0.7f;

        public static event System.Action Changed;
        public static bool MusicMuted => PlayerPrefs.GetInt(MusicMutedKey, 0) != 0;
        public static bool SoundMuted => PlayerPrefs.GetInt(SoundMutedKey, 0) != 0;
        public static float MusicVolume => MusicMuted ? 0f : Sanitize(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
        public static float SoundVolume => SoundMuted ? 0f : Sanitize(PlayerPrefs.GetFloat(SoundVolumeKey, DefaultSoundVolume));

        public static float Sanitize(float value) => float.IsNaN(value) || float.IsInfinity(value)
            ? 0f : Mathf.Clamp01(value);

        public static void SetMusicVolume(float value)
        {
            PlayerPrefs.SetFloat(MusicVolumeKey, Sanitize(value));
            PlayerPrefs.SetInt(MusicMutedKey, 0);
            Changed?.Invoke();
        }

        public static void SetSoundVolume(float value)
        {
            PlayerPrefs.SetFloat(SoundVolumeKey, Sanitize(value));
            PlayerPrefs.SetInt(SoundMutedKey, 0);
            Changed?.Invoke();
        }

        public static void SetMusicMuted(bool muted)
        {
            PlayerPrefs.SetInt(MusicMutedKey, muted ? 1 : 0);
            Changed?.Invoke();
        }

        public static void SetSoundMuted(bool muted)
        {
            PlayerPrefs.SetInt(SoundMutedKey, muted ? 1 : 0);
            Changed?.Invoke();
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
