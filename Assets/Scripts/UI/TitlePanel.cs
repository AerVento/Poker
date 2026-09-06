using Framework.UI;
using Game.Network;
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
        [SerializeField]
        private Button _tasks;

        private void OnEnable()
        {
            GameAudioManager.EnsureClient()?.EnterLobby();
            _start.onClick.AddListener(Play);
            _tasks.onClick.AddListener(Tasks);
            _option.onClick.AddListener(Option);
            _quit.onClick.AddListener(Quit);
        }

        private void OnDisable()
        {
            _start.onClick.RemoveListener(Play);
            _tasks.onClick.RemoveListener(Tasks);
            _option.onClick.RemoveListener(Option);
            _quit.onClick.RemoveListener(Quit);
        }

        private void Update()
        {
            _start.interactable = !GameController.Available || !GameController.Instance.IsNetworkInitializing;
            _tasks.interactable = _start.interactable &&
                !(NetworkManager.singleton is NetworkRoom room && room.IsRoomEntryPending);
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

        private void Tasks()
        {
            if (GameController.Available && GameController.Instance.HasNetworkInitializationStarted &&
                !GameController.Instance.IsClientNetworkReady)
            {
                GameController.Instance.RetryClientNetworkInitialization();
                return;
            }
            if (!(NetworkManager.singleton is NetworkRoom room) || room.IsRoomEntryPending)
                return;
            if (room.CanOpenDailyTasks)
            {
                UIManager.Instance.PushPanel<DailyTaskPanel>();
                return;
            }
            if (NetworkClient.connection != null && NetworkClient.connection.isAuthenticated)
                return;
            UIManager.Instance.PushPanel<LoginPanel>();
            LoginPanel.Instance.BeginTaskLogin();
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
