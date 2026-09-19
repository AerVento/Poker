using Framework.UI;
using Game.Network;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class LobbyPanel : SingletonPanel<LobbyPanel>
    {
        [Header("Account")]
        [SerializeField] private TextMeshProUGUI _userName;
        [SerializeField] private TextMeshProUGUI _dollars;
        [SerializeField] private TextMeshProUGUI _chips;
        private string _displayedUserName;
        private uint? _displayedDollars;
        private uint? _displayedChips;

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
            if (_dollars != null && _displayedDollars != state.Dollars)
            {
                _displayedDollars = state.Dollars;
                _dollars.text = $"${state.Dollars:N0}";
            }
            if (_chips != null && _displayedChips != state.Chips)
            {
                _displayedChips = state.Chips;
                _chips.text = $"${state.Chips:N0}";
            }
        }

        private void Back() => UIManager.Instance.PopPanel(Identifier);

        private void OpenRooms()
        {
            if (_rooms.interactable)
                UIManager.Instance.PushPanel<RoomListPanel>();
        }

        private void OpenLeaderboard() => OpenAccountPage(LoginDestination.Leaderboard);

        private void OpenAchievements() => OpenAccountPage(LoginDestination.Achievements);

        private void OpenTasks() => OpenAccountPage(LoginDestination.DailyTasks);

        private void OpenShop() => OpenAccountPage(LoginDestination.Shop);

        private void OpenCollection() => OpenAccountPage(LoginDestination.Collection);

        private void OpenSettings() => UIManager.Instance.PushPanel<OptionPanel>();

        private void OpenExchange() => OpenAccountPage(LoginDestination.Exchange);

        private void OpenAccountPage(LoginDestination destination)
        {
            if (!(NetworkManager.singleton is NetworkRoom room) || room.IsRoomEntryPending)
                return;

            bool canOpen = destination switch
            {
                LoginDestination.Shop or LoginDestination.Collection => room.CanOpenShop,
                LoginDestination.Achievements => room.CanViewAchievements,
                LoginDestination.Leaderboard => room.CanOpenLeaderboard,
                LoginDestination.DailyTasks => room.CanOpenDailyTasks,
                LoginDestination.Exchange => room.CanOpenWallet,
                _ => false,
            };
            if (canOpen)
                ShowAccountPage(destination);
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
                case LoginDestination.Exchange:
                    UIManager.Instance.PushPanel<ChipExchangePanel>();
                    break;
            }
        }
    }
}
