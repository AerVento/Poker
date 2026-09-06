using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Framework.UI;
using UnityEngine.UI;
using TMPro;
using Game.Network;
using Mirror;
namespace Game.UI
{
    public abstract class SettingsPanel<T> : SingletonPanel<T> where T : MonoBehaviour
    {
        [SerializeField]
        private Button _back;

        [SerializeField]
        private Toggle _fullscreen;

        [SerializeField]
        private TMP_Dropdown _resolution;

        [SerializeField]
        private TextMeshProUGUI _userName;

        [SerializeField]
        private Button _logout;

        [SerializeField]
        private Slider _musicVolume;
        [SerializeField]
        private Slider _soundVolume;
        [SerializeField]
        private TextMeshProUGUI _musicVolumeLabel;
        [SerializeField]
        private TextMeshProUGUI _soundVolumeLabel;
        [SerializeField]
        private Button _audioCredits;

        protected virtual bool AllowLogout => false;

        private List<Resolution> _availableResolutions = new List<Resolution>();

        protected void RefreshResolutions()
        {
            _availableResolutions.Clear();
            foreach (var resolution in Screen.resolutions)
            {
                bool isDuplicated = false;
                foreach(var added in _availableResolutions)
                {
                    if(added.width == resolution.width &&  added.height == resolution.height)
                    {
                        isDuplicated = true;
                        break;
                    }
                }
                if (isDuplicated)
                    continue;
                else
                    _availableResolutions.Add(resolution);
            }

            int selected = _availableResolutions.FindIndex(resolution =>
                resolution.width == Screen.width && resolution.height == Screen.height);
            if (selected < 0)
            {
                selected = _availableResolutions.Count;
                _availableResolutions.Add(new Resolution { width = Screen.width, height = Screen.height });
            }

            _resolution.ClearOptions();
            foreach(var resolution in _availableResolutions)
            {
                var option = new TMP_Dropdown.OptionData($"{resolution.width}x{resolution.height}");
                _resolution.options.Add(option);
            }
            _resolution.SetValueWithoutNotify(selected);
            _resolution.RefreshShownValue();
        }

        protected virtual void OnEnable()
        {
            _back.onClick.AddListener(Back);
            if (_musicVolume != null)
            {
                _musicVolume.SetValueWithoutNotify(AudioPreferences.MusicVolume);
                _musicVolume.onValueChanged.AddListener(ChangeMusicVolume);
            }
            if (_soundVolume != null)
            {
                _soundVolume.SetValueWithoutNotify(AudioPreferences.SoundVolume);
                _soundVolume.onValueChanged.AddListener(ChangeSoundVolume);
            }
            if (_audioCredits != null)
                _audioCredits.onClick.AddListener(ShowAudioCredits);
            RefreshAudioLabels();
            if (_logout != null && AllowLogout)
                _logout.onClick.AddListener(Logout);

            var isAuthenticated = GameController.Available &&
                GameController.Instance.State.IsAuthenticated;
            _userName.text = isAuthenticated
                ? GameController.Instance.State.UserName
                : "未登录";
            if (_logout != null)
            {
                _logout.gameObject.SetActive(AllowLogout && isAuthenticated);
                _logout.interactable = AllowLogout && isAuthenticated;
            }

            RefreshResolutions();
            _resolution.onValueChanged.AddListener(ChangeResolution);

            _fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
            _fullscreen.onValueChanged.AddListener(ChangeFullscreen);
        }
        protected virtual void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            if (_musicVolume != null)
                _musicVolume.onValueChanged.RemoveListener(ChangeMusicVolume);
            if (_soundVolume != null)
                _soundVolume.onValueChanged.RemoveListener(ChangeSoundVolume);
            if (_audioCredits != null)
                _audioCredits.onClick.RemoveListener(ShowAudioCredits);
            GameAudioManager.Existing?.SaveVolumes();
            if (_logout != null)
                _logout.onClick.RemoveListener(Logout);
            _resolution.onValueChanged.RemoveListener(ChangeResolution);
            _fullscreen.onValueChanged.RemoveListener(ChangeFullscreen);
        }

        private void RefreshAudioLabels()
        {
            if (_musicVolumeLabel != null)
                _musicVolumeLabel.text = $"{Mathf.RoundToInt(AudioPreferences.MusicVolume * 100)}%";
            if (_soundVolumeLabel != null)
                _soundVolumeLabel.text = $"{Mathf.RoundToInt(AudioPreferences.SoundVolume * 100)}%";
        }

        private void ChangeMusicVolume(float value)
        {
            var audio = GameAudioManager.EnsureClient();
            if (audio != null)
                audio.SetMusicVolume(value);
            else
                AudioPreferences.SetMusicVolume(value);
            RefreshAudioLabels();
        }

        private void ChangeSoundVolume(float value)
        {
            var audio = GameAudioManager.EnsureClient();
            if (audio != null)
                audio.SetSoundVolume(value);
            else
                AudioPreferences.SetSoundVolume(value);
            RefreshAudioLabels();
        }

        private void ShowAudioCredits()
        {
            MsgBox.Create("AudioCredits", box =>
            {
                box.Title = "音频致谢";
                box.Message = "音乐：Lobby Time / Covert Affair / Crypto\n" +
                    "Kevin MacLeod (incompetech.com)\n" +
                    "Creative Commons Attribution 4.0\n" +
                    "https://creativecommons.org/licenses/by/4.0/\n" +
                    "已剪辑为循环片段并调整音量。\n\n" +
                    "牌桌音效：Kenney (kenney.nl)，CC0。";
                box.Type = MsgboxType.Confirm;
                box.ConfirmButtonText = "返回";
            });
        }

        protected void Back()
        {
            UIManager.Instance.PopPanel(Identifier);
        }

        private void Logout()
        {
            if (!AllowLogout)
                return;
            _logout.interactable = false;
            if (NetworkManager.singleton != null &&
                NetworkManager.singleton.TryGetComponent<AccountAuthenticator>(out var authenticator))
            {
                authenticator.Logout();
            }
        }

        private void ChangeResolution(int option)
        {
            if (option < 0 || option >= _availableResolutions.Count)
                return;
            var resolution = _availableResolutions[option];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
            PlayerPrefs.SetInt("Screen_Width", resolution.width);
            PlayerPrefs.SetInt("Screen_Height", resolution.height);
        }

        private void ChangeFullscreen(bool value)
        {
            Screen.SetResolution(Screen.width, Screen.height, value);
            PlayerPrefs.SetInt("Screen_Fullscreen", value ? 1 : 0);
        }
    }
}
