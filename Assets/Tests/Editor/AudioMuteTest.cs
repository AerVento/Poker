using System.Collections.Generic;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class AudioMuteTest
    {
        private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();

        [SetUp]
        public void SavePreferences()
        {
            _floats.Clear();
            _ints.Clear();
            foreach (var key in new[] { AudioPreferences.MusicVolumeKey, AudioPreferences.SoundVolumeKey })
            {
                if (PlayerPrefs.HasKey(key)) _floats.Add(key, PlayerPrefs.GetFloat(key));
                PlayerPrefs.DeleteKey(key);
            }
            foreach (var key in new[] { AudioPreferences.MusicMutedKey, AudioPreferences.SoundMutedKey })
            {
                if (PlayerPrefs.HasKey(key)) _ints.Add(key, PlayerPrefs.GetInt(key));
                PlayerPrefs.DeleteKey(key);
            }
        }

        [TearDown]
        public void RestorePreferences()
        {
            foreach (var key in new[] { AudioPreferences.MusicVolumeKey, AudioPreferences.SoundVolumeKey })
            {
                PlayerPrefs.DeleteKey(key);
                if (_floats.TryGetValue(key, out var value)) PlayerPrefs.SetFloat(key, value);
            }
            foreach (var key in new[] { AudioPreferences.MusicMutedKey, AudioPreferences.SoundMutedKey })
            {
                PlayerPrefs.DeleteKey(key);
                if (_ints.TryGetValue(key, out var value)) PlayerPrefs.SetInt(key, value);
            }
            PlayerPrefs.Save();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MuteRestoresPreviousVolumeAndDoesNotAffectOtherChannel(bool music)
        {
            AudioPreferences.SetMusicVolume(0.42f);
            AudioPreferences.SetSoundVolume(0.63f);
            if (music) AudioPreferences.SetMusicMuted(true);
            else AudioPreferences.SetSoundMuted(true);
            Assert.AreEqual(0f, music ? AudioPreferences.MusicVolume : AudioPreferences.SoundVolume);
            Assert.AreEqual(music ? 0.63f : 0.42f, music ? AudioPreferences.SoundVolume : AudioPreferences.MusicVolume);
            if (music) AudioPreferences.SetMusicMuted(true);
            else AudioPreferences.SetSoundMuted(true);
            AudioPreferences.Save();
            Assert.AreEqual(music ? 0.42f : 0.63f,
                PlayerPrefs.GetFloat(music ? AudioPreferences.MusicVolumeKey : AudioPreferences.SoundVolumeKey));
            if (music) AudioPreferences.SetMusicMuted(false);
            else AudioPreferences.SetSoundMuted(false);
            Assert.AreEqual(music ? 0.42f : 0.63f, music ? AudioPreferences.MusicVolume : AudioPreferences.SoundVolume);
        }

        [Test]
        public void ChangingOneSliderUnmutesOnlyThatChannel()
        {
            AudioPreferences.SetMusicMuted(true);
            AudioPreferences.SetSoundMuted(true);
            AudioPreferences.SetMusicVolume(0.8f);
            Assert.IsFalse(AudioPreferences.MusicMuted);
            Assert.IsTrue(AudioPreferences.SoundMuted);
            Assert.AreEqual(0.8f, AudioPreferences.MusicVolume);
            Assert.AreEqual(0f, AudioPreferences.SoundVolume);
        }

        [Test]
        public void ExistingVolumePreferencesAndZeroVolumeRemainValid()
        {
            PlayerPrefs.SetFloat(AudioPreferences.MusicVolumeKey, 0.28f);
            Assert.AreEqual(0.28f, AudioPreferences.MusicVolume);
            Assert.IsFalse(AudioPreferences.MusicMuted);
            AudioPreferences.SetSoundVolume(0f);
            AudioPreferences.SetSoundMuted(true);
            AudioPreferences.SetSoundMuted(false);
            Assert.AreEqual(0f, AudioPreferences.SoundVolume);
        }

        [Test]
        public void PreferenceChangesNotifyAudioConsumers()
        {
            var notifications = 0;
            System.Action changed = () => notifications++;
            AudioPreferences.Changed += changed;
            try
            {
                AudioPreferences.SetMusicMuted(true);
                AudioPreferences.SetSoundMuted(true);
                AudioPreferences.SetMusicVolume(0.5f);
                AudioPreferences.SetSoundVolume(0.5f);
                Assert.AreEqual(4, notifications);
            }
            finally
            {
                AudioPreferences.Changed -= changed;
            }
        }

        [TestCase("OptionPanel")]
        [TestCase("InGameOptionPanel")]
        public void BothSettingsPrefabsHaveTwoIconButtonsAndCompleteReferences(string name)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/OptionPanel/" + name + ".prefab");
            var script = new SerializedObject(root.GetComponent(name == "OptionPanel" ? typeof(OptionPanel) : typeof(InGameOptionPanel)));
            var music = (Button)script.FindProperty("_musicMute").objectReferenceValue;
            var sound = (Button)script.FindProperty("_soundMute").objectReferenceValue;
            Assert.IsNotNull(music);
            Assert.IsNotNull(sound);
            Assert.AreNotSame(music, sound);
            Assert.AreEqual(0, music.onClick.GetPersistentEventCount());
            Assert.AreEqual(0, sound.onClick.GetPersistentEventCount());
            Assert.IsNotNull(script.FindProperty("_audioOnIcon").objectReferenceValue);
            Assert.IsNotNull(script.FindProperty("_audioMutedIcon").objectReferenceValue);
            Assert.GreaterOrEqual(music.GetComponent<LayoutElement>().preferredWidth, 56f);
            Assert.GreaterOrEqual(sound.GetComponent<LayoutElement>().preferredWidth, 56f);
        }
    }
}
