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
        [SerializeField]
        private Button _option;
        [SerializeField]
        private Button _quit;

        private void OnEnable()
        {
            _start.onClick.AddListener(Play);
            _option.onClick.AddListener(Option);
            _quit.onClick.AddListener(Quit);
        }

        private void OnDisable()
        {
            _start.onClick.RemoveListener(Play);
            _option.onClick.RemoveListener(Option);
            _quit.onClick.RemoveListener(Quit);
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

        private void Option()
        {
            UIManager.Instance.HidePanel<TitlePanel>();
            UIManager.Instance.ShowPanel<OptionPanel>();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
