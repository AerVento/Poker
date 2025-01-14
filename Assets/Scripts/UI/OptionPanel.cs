using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Framework.UI;
using UnityEngine.UI;
using static Mirror.NetworkRuntimeProfiler;
using TMPro;
namespace Game.UI
{
    public class OptionPanel : SingletonPanel<OptionPanel>
    {
        [SerializeField]
        private Button _back;

        [SerializeField]
        private TMP_InputField _nickname;

        private void OnEnable()
        {
            _back.onClick.AddListener(Back);
            _nickname.text = GameController.Instance.State.Nickname;
            _nickname.onValueChanged.AddListener(NicknameChange);
        }
        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _nickname.onValueChanged.RemoveListener(NicknameChange);
        }

        private void Back()
        {
            UIManager.Instance.HidePanel<OptionPanel>();
            UIManager.Instance.ShowPanel<TitlePanel>();
        }

        private void NicknameChange(string newValue)
        {
            GameController.Instance.State.Nickname = newValue;
        }
    }
}