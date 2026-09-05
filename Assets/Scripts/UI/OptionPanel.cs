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
    public class OptionPanel : SingletonPanel<OptionPanel>
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

        private List<Resolution> _availableResolutions = new List<Resolution>();

        private void RefreshResolutions()
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

        private void OnEnable()
        {
            _back.onClick.AddListener(Back);
            _logout.onClick.AddListener(Logout);

            var isAuthenticated = GameController.Available &&
                GameController.Instance.State.IsAuthenticated;
            _userName.text = isAuthenticated
                ? GameController.Instance.State.UserName
                : "未登录";
            _logout.gameObject.SetActive(isAuthenticated);
            _logout.interactable = isAuthenticated;

            RefreshResolutions();
            _resolution.onValueChanged.AddListener(ChangeResolution);

            _fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
            _fullscreen.onValueChanged.AddListener(ChangeFullscreen);
        }
        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _logout.onClick.RemoveListener(Logout);
            _resolution.onValueChanged.RemoveListener(ChangeResolution);
            _fullscreen.onValueChanged.RemoveListener(ChangeFullscreen);
        }

        private void Back()
        {
            UIManager.Instance.PopPanel<OptionPanel>();
        }

        private void Logout()
        {
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
