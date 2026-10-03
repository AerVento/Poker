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
        private void OnEnable()
        {
            GameAudioManager.EnsureClient()?.EnterLobby();
            _start.onClick.AddListener(Play);
            _option.onClick.AddListener(Option);
            RefreshControls();
        }

        private void OnDisable()
        {
            _start.onClick.RemoveListener(Play);
            _option.onClick.RemoveListener(Option);
        }

        private void Update() => RefreshControls();

        private void RefreshControls()
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
                UIManager.Instance.PushPanel<LobbyPanel>();
                return;
            }

            UIManager.Instance.PushPanel<LoginPanel>();
            LoginPanel.Instance.BeginSavedLogin();
        }

        private void Option()
        {
            UIManager.Instance.PushPanel<OptionPanel>();
        }

    }
}
