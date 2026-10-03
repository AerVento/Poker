using Cysharp.Threading.Tasks;
using Framework.UI;
using Game.Content;
using Game.Network;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class LobbyPanel : SingletonPanel<LobbyPanel>
    {
        private enum AccountPage { DailyTasks, Leaderboard, Shop, Collection, Achievements, Exchange }

        [Header("Account")]
        [SerializeField] private TextMeshProUGUI _userName;
        private string _displayedUserName;

        [Header("Navigation")]
        [SerializeField] private Button _back;

        [Header("Destinations")]
        [SerializeField] private Button _rooms;
        [SerializeField] private Button _leaderboard;
        [SerializeField] private Button _achievements;
        [SerializeField] private Button _tasks;
        [SerializeField] private Button _shop;
        [SerializeField] private Button _collection;
        [SerializeField] private Button _settings;
        [SerializeField] private Button _exchange;

        private void OnEnable()
        {
            GameAudioManager.EnsureClient()?.EnterLobby();
            _back.onClick.AddListener(Back);
            _rooms.onClick.AddListener(OpenRooms);
            _leaderboard.onClick.AddListener(OpenLeaderboard);
            _achievements.onClick.AddListener(OpenAchievements);
            _tasks.onClick.AddListener(OpenTasks);
            _shop.onClick.AddListener(OpenShop);
            _collection.onClick.AddListener(OpenCollection);
            _settings.onClick.AddListener(OpenSettings);
            _exchange.onClick.AddListener(OpenExchange);
            RefreshControls();
            CheckForUpdates().Forget();
        }

        // Each lobby entry: an outdated page only asks for a refresh; otherwise newer remote content updates in the background.
        private async UniTaskVoid CheckForUpdates()
        {
            try
            {
                if (await PageUpdatePrompt.EnsureCurrentAsync(this.GetCancellationTokenOnDestroy()))
                    await RemoteAssets.CheckForUpdatesAsync();
            }
            catch (System.OperationCanceledException) { }
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _rooms.onClick.RemoveListener(OpenRooms);
            _leaderboard.onClick.RemoveListener(OpenLeaderboard);
            _achievements.onClick.RemoveListener(OpenAchievements);
            _tasks.onClick.RemoveListener(OpenTasks);
            _shop.onClick.RemoveListener(OpenShop);
            _collection.onClick.RemoveListener(OpenCollection);
            _settings.onClick.RemoveListener(OpenSettings);
            _exchange.onClick.RemoveListener(OpenExchange);
        }

        private void Update() => RefreshControls();

        private void RefreshControls()
        {
            RefreshAccountStatus();
            var room = NetworkManager.singleton as NetworkRoom;
            bool authenticated = NetworkClient.connection != null && NetworkClient.connection.isAuthenticated &&
                GameController.Available && GameController.Instance.State.IsAuthenticated;
            bool available = authenticated && room != null && !room.IsRoomEntryPending;
            _rooms.interactable = available;
            _leaderboard.interactable = available && room.CanOpenLeaderboard;
            _achievements.interactable = available && room.CanViewAchievements;
            _tasks.interactable = available && room.CanOpenDailyTasks;
            _shop.interactable = available && room.CanOpenShop;
            _collection.interactable = available && room.CanOpenShop;
            _exchange.interactable = available && room.CanOpenWallet;
            _settings.interactable = true;
        }

        private void RefreshAccountStatus()
        {
            if (!GameController.Available)
                return;

            var state = GameController.Instance.State;
            if (_userName != null && _displayedUserName != state.UserName)
            {
                _displayedUserName = state.UserName;
                _userName.text = state.UserName ?? string.Empty;
            }
        }

        private void Back() => UIManager.Instance.PopPanel(Identifier);

        private void OpenRooms()
        {
            if (_rooms.interactable)
                UIManager.Instance.PushPanel<RoomListPanel>();
        }

        private void OpenLeaderboard() => OpenAccountPage(AccountPage.Leaderboard);

        private void OpenAchievements() => OpenAccountPage(AccountPage.Achievements);

        private void OpenTasks() => OpenAccountPage(AccountPage.DailyTasks);

        private void OpenShop() => OpenAccountPage(AccountPage.Shop);

        private void OpenCollection() => OpenAccountPage(AccountPage.Collection);

        private void OpenSettings() => UIManager.Instance.PushPanel<OptionPanel>();

        private void OpenExchange() => OpenAccountPage(AccountPage.Exchange);

        private void OpenAccountPage(AccountPage destination)
        {
            if (!(NetworkManager.singleton is NetworkRoom room) || room.IsRoomEntryPending)
                return;

            bool canOpen = destination switch
            {
                AccountPage.Shop or AccountPage.Collection => room.CanOpenShop,
                AccountPage.Achievements => room.CanViewAchievements,
                AccountPage.Leaderboard => room.CanOpenLeaderboard,
                AccountPage.DailyTasks => room.CanOpenDailyTasks,
                AccountPage.Exchange => room.CanOpenWallet,
                _ => false,
            };
            if (canOpen)
                ShowAccountPage(destination);
        }

        private static void ShowAccountPage(AccountPage destination)
        {
            switch (destination)
            {
                case AccountPage.Achievements:
                    UIManager.Instance.PushPanel<AchievementPanel>();
                    break;
                case AccountPage.DailyTasks:
                    UIManager.Instance.PushPanel<DailyTaskPanel>();
                    break;
                case AccountPage.Leaderboard:
                    UIManager.Instance.PushPanel<LeaderboardPanel>();
                    break;
                case AccountPage.Shop:
                case AccountPage.Collection:
                    var shop = UIManager.Instance.PushPanel<ShopPanel>();
                    shop.Open(destination == AccountPage.Collection ? ShopPageMode.Collection : ShopPageMode.Shop);
                    shop.BeginOnline();
                    break;
                case AccountPage.Exchange:
                    UIManager.Instance.PushPanel<ChipExchangePanel>();
                    break;
            }
        }
    }
}
