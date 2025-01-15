using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Framework.UI;
using UnityEngine.UI;
using TMPro;
using Mirror;
using Game.Network;
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
        private TMP_InputField _nickname;

        private List<Resolution> _availableResolutions = new List<Resolution>();

        private void Start()
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

            _resolution.ClearOptions();
            foreach(var resolution in _availableResolutions)
            {
                var option = new TMP_Dropdown.OptionData($"{resolution.width}x{resolution.height}");
                _resolution.options.Add(option);
            }
        }

        private void OnEnable()
        {
            _back.onClick.AddListener(Back);

            _nickname.text = GameController.Instance.State.Nickname;
            _nickname.onEndEdit.AddListener(NicknameChange);

            var resolutions = Screen.resolutions;
            for (int i = 0; i < resolutions.Length; i++)
            {
                var resolution = resolutions[i];
                if (Screen.width == resolution.width && Screen.height == resolution.height)
                {
                    _resolution.SetValueWithoutNotify(i);
                    break;
                }
            }
            _resolution.onValueChanged.AddListener(ChangeResolution);

            _fullscreen.isOn = Screen.fullScreen;
            _fullscreen.onValueChanged.AddListener(ChangeFullscreen);
        }
        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _nickname.onEndEdit.RemoveListener(NicknameChange);
        }

        private void Back()
        {
            UIManager.Instance.HidePanel<OptionPanel>();
            UIManager.Instance.ShowPanel<TitlePanel>();
        }

        private void NicknameChange(string newValue)
        {
            if (!string.IsNullOrEmpty(newValue))
            {
                if (NetworkClient.isConnected)
                    NetworkClient.Send(new PlayerChangeNameMessage()
                    {
                        OldName = _nickname.text,
                        NewName = newValue
                    });
                _nickname.text = newValue;
                GameController.Instance.State.Nickname = newValue;
            }
        }

        private void ChangeResolution(int option)
        {
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