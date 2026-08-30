using Framework.UI;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class TitlePanel : SingletonPanel<TitlePanel>
    {
        [SerializeField]
        private Button _start;

        private void OnEnable()
        {
            _start.onClick.AddListener(Play);
        }

        private void OnDisable()
        {
            _start.onClick.RemoveListener(Play);
        }

        private void Play()
        {
            if (NetworkClient.connection != null &&
                NetworkClient.connection.isAuthenticated &&
                GameController.Available &&
                GameController.Instance.State.IsAuthenticated)
            {
                UIManager.Instance.HidePanel<TitlePanel>();
                UIManager.Instance.ShowPanel<RoomListPanel>();
                return;
            }

            UIManager.Instance.HidePanel<TitlePanel>();
            UIManager.Instance.ShowPanel<LoginPanel>();
            LoginPanel.Instance.BeginSavedLogin();
        }
    }
}
