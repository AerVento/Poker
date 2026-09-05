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

        private void Update()
        {
            _start.interactable = !GameController.Available || !GameController.Instance.IsNetworkInitializing;
        }

        private void Play()
        {
            if (GameController.Available && GameController.Instance.HasNetworkInitializationStarted &&
                !GameController.Instance.IsClientNetworkReady)
            {
                GameController.Instance.RetryClientNetworkInitialization();
                return;
            }

            if (NetworkClient.isConnected && NetworkClient.connection != null &&
                NetworkClient.connection.isAuthenticated &&
                GameController.Available &&
                GameController.Instance.State.IsAuthenticated)
            {
                UIManager.Instance.PushPanel<RoomListPanel>();
                return;
            }

            UIManager.Instance.PushPanel<LoginPanel>();
            LoginPanel.Instance.BeginSavedLogin();
        }

        private void Option()
        {
            UIManager.Instance.PushPanel<OptionPanel>();
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
