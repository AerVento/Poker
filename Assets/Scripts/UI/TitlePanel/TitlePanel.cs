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
        [SerializeField] private Button _shop;
        [SerializeField] private Button _collection;
        [SerializeField] private Button _leaderboard;
        [SerializeField] private Button _achievements;

        private void OnEnable()
        {
            GameAudioManager.EnsureClient()?.EnterLobby();
            _start.onClick.AddListener(Play);
            _tasks.onClick.AddListener(Tasks);
            _shop.onClick.AddListener(Shop);
            _collection.onClick.AddListener(Collection);
            _leaderboard.onClick.AddListener(Leaderboard);
            _achievements.onClick.AddListener(Achievements);
            _option.onClick.AddListener(Option);
            _quit.onClick.AddListener(Quit);
            RefreshControls();
        }

        private void OnDisable()
        {
            _start.onClick.RemoveListener(Play);
            _tasks.onClick.RemoveListener(Tasks);
            _shop.onClick.RemoveListener(Shop);
            _collection.onClick.RemoveListener(Collection);
            _leaderboard.onClick.RemoveListener(Leaderboard);
            _achievements.onClick.RemoveListener(Achievements);
            _option.onClick.RemoveListener(Option);
            _quit.onClick.RemoveListener(Quit);
        }

        private void Update() => RefreshControls();

        private void RefreshControls()
        {
            _start.interactable = !GameController.Available || !GameController.Instance.IsNetworkInitializing;
            _tasks.interactable = _start.interactable &&
                !(NetworkManager.singleton is NetworkRoom room && room.IsRoomEntryPending);
            _shop.interactable = _tasks.interactable;
            _collection.interactable = _tasks.interactable;
            _leaderboard.interactable = _tasks.interactable;
            _achievements.interactable = _tasks.interactable;
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

        private void Achievements() => OpenAccountPage(LoginDestination.Achievements);
        private void Tasks() => OpenAccountPage(LoginDestination.DailyTasks);
        private void Shop() => OpenAccountPage(LoginDestination.Shop);
        private void Collection() => OpenAccountPage(LoginDestination.Collection);
        private void Leaderboard() => OpenAccountPage(LoginDestination.Leaderboard);

        private void OpenAccountPage(LoginDestination destination)
        {
            if (GameController.Available && GameController.Instance.HasNetworkInitializationStarted &&
                !GameController.Instance.IsClientNetworkReady)
            {
                GameController.Instance.RetryClientNetworkInitialization();
                return;
            }
            if (!(NetworkManager.singleton is NetworkRoom room) || room.IsRoomEntryPending)
                return;
            var canOpen = destination == LoginDestination.Shop || destination == LoginDestination.Collection ? room.CanOpenShop :
                destination == LoginDestination.Achievements ? room.CanViewAchievements :
                destination == LoginDestination.Leaderboard ? room.CanOpenLeaderboard : room.CanOpenDailyTasks;
            if (canOpen)
            {
                ShowAccountPage(destination);
                return;
            }
            if (NetworkClient.connection != null && NetworkClient.connection.isAuthenticated)
                return;
            var login = UIManager.Instance.PushPanel<LoginPanel>();
            login.BeginAccountLogin(destination);
        }

        internal static void ShowAccountPage(LoginDestination destination)
        {
            switch (destination)
            {
                case LoginDestination.Achievements:
                    UIManager.Instance.PushPanel<AchievementPanel>();
                    break;
                case LoginDestination.DailyTasks:
                    UIManager.Instance.PushPanel<DailyTaskPanel>();
                    break;
                case LoginDestination.Leaderboard:
                    UIManager.Instance.PushPanel<LeaderboardPanel>();
                    break;
                case LoginDestination.Shop:
                case LoginDestination.Collection:
                    var shop = UIManager.Instance.PushPanel<ShopPanel>();
                    shop.Open(destination == LoginDestination.Collection ? ShopPageMode.Collection : ShopPageMode.Shop);
                    shop.BeginOnline();
                    break;
            }
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
