using Framework.UI;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    public class TitlePanel : SingletonPanel<TitlePanel>
    {
        [SerializeField]
        private TextMeshProUGUI _onlineStatus;
        [SerializeField]
        private Button _start;
        [SerializeField] 
        private Button _option;
        [SerializeField]
        private Button _quit;

        private void Start()
        {

        }

        private void OnEnable()
        {
            _start.onClick.AddListener(Play);
            _option.onClick.AddListener(Option);
            _quit.onClick.AddListener(Quit);
        }

        private void Update()
        {
            if (NetworkClient.isConnected)
            {
                _onlineStatus.color = Color.green;
                _onlineStatus.text = "在线";
            }
            else
            {
                _onlineStatus.color = Color.yellow;
                _onlineStatus.text = "离线";
            }
        }

        private void OnDisable()
        {
            _start.onClick.RemoveListener(Play);
            _option.onClick.RemoveListener(Option);
            _quit.onClick.RemoveListener(Quit);
        }

        private void Play()
        {
            if (!NetworkClient.isConnected)
            {
                // 没连接的话，先连接
                NetworkManager.singleton.StartClient();
            }
            else
                UIManager.Instance.ShowPanel<RoomListPanel>();
        }

        private void Option()
        {
            UIManager.Instance.HidePanel<TitlePanel>();
            UIManager.Instance.ShowPanel<OptionPanel>();
        }

        private void Quit()
        {
            Application.Quit();
        }
    }

}
