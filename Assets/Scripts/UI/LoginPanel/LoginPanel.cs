using Framework.UI;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    internal enum LoginDestination
    {
        Lobby,
        DailyTasks,
        Leaderboard,
        Shop,
        Collection,
        Achievements,
        Exchange,
    }

    internal enum LoginMode
    {
        Login,
        Register,
    }

    public class LoginPanel : SingletonPanel<LoginPanel>
    {
        [Header("Inputs")]
        [SerializeField]
        private TMP_InputField _userName;
        [SerializeField]
        private TMP_InputField _password;
        [SerializeField]
        private TMP_InputField _passwordConfirmation;
        [SerializeField]
        private GameObject _passwordConfirmationGroup;

        [Header("Buttons")]
        [SerializeField]
        private Button _loginTab;
        [SerializeField]
        private Button _registerTab;
        [SerializeField]
        private Button _submit;
        [SerializeField]
        private Button _back;

        [Header("Labels")]
        [SerializeField]
        private TextMeshProUGUI _submitLabel;
        [SerializeField]
        private TextMeshProUGUI _status;
        [SerializeField]
        private TextMeshProUGUI _title;

        [Header("Tab State")]
        [SerializeField]
        private GameObject _loginTabSelected;
        [SerializeField]
        private GameObject _registerTabSelected;

        private AccountAuthenticator _authenticator;
        private bool _subscribed;
        private bool _savedLoginStarted;
        private LoginMode _mode;

        internal LoginDestination DestinationAfterLogin { get; private set; }

        private void OnEnable()
        {
            _loginTab.onClick.AddListener(ShowLogin);
            _registerTab.onClick.AddListener(ShowRegister);
            _submit.onClick.AddListener(Submit);
            _back.onClick.AddListener(Back);
            _password.contentType = TMP_InputField.ContentType.Password;
            _password.ForceLabelUpdate();
            _passwordConfirmation.contentType = TMP_InputField.ContentType.Password;
            _passwordConfirmation.ForceLabelUpdate();
            _userName.text = LoginCredentialStore.SavedUserName;
            SetMode(LoginMode.Login);
            SetStatus(string.Empty);
            ResolveAuthenticator();
            RefreshControls();
        }

        private void Update() => RefreshControls();

        private void RefreshControls()
        {
            ResolveAuthenticator();
            var pending = _authenticator != null && _authenticator.IsAuthenticationPending;
            _loginTab.interactable = !pending && _mode != LoginMode.Login;
            _registerTab.interactable = !pending && _mode != LoginMode.Register;
            _submit.interactable = !pending;
            _back.interactable = true;
            _userName.interactable = !pending;
            _password.interactable = !pending;
            _passwordConfirmation.interactable = !pending;
        }

        private void OnDisable()
        {
            _loginTab.onClick.RemoveListener(ShowLogin);
            _registerTab.onClick.RemoveListener(ShowRegister);
            _submit.onClick.RemoveListener(Submit);
            _back.onClick.RemoveListener(Back);
            Unsubscribe();
            _savedLoginStarted = false;
            DestinationAfterLogin = LoginDestination.Lobby;
            _password.text = string.Empty;
            _passwordConfirmation.text = string.Empty;
        }

        public void BeginTaskLogin() => BeginAccountLogin(LoginDestination.DailyTasks);

        internal void BeginAccountLogin(LoginDestination destination)
        {
            DestinationAfterLogin = destination;
            BeginSavedLogin();
        }

        public void BeginSavedLogin(string sessionToken = null)
        {
            if (_savedLoginStarted)
                return;

            _savedLoginStarted = true;
            ResolveAuthenticator();
            if (!string.IsNullOrEmpty(sessionToken) && GameController.Available)
                _userName.text = GameController.Instance.State.UserName;
            if (_authenticator != null && _authenticator.LoginWithSavedCredentials(sessionToken))
                SetStatus("正在恢复登录状态……");
        }

        private void ShowLogin() => SetMode(LoginMode.Login);

        private void ShowRegister() => SetMode(LoginMode.Register);

        private void Submit() => Authenticate(_mode == LoginMode.Register
            ? AccountAuthenticationOperation.Register
            : AccountAuthenticationOperation.Login);

        private void Authenticate(AccountAuthenticationOperation operation)
        {
            ResolveAuthenticator();
            if (_authenticator == null)
            {
                SetStatus("登录服务尚未准备好。");
                return;
            }
            if (string.IsNullOrEmpty(_userName.text) || string.IsNullOrEmpty(_password.text))
            {
                SetStatus("用户名和密码不能为空。");
                return;
            }
            if (operation == AccountAuthenticationOperation.Register &&
                _password.text != _passwordConfirmation.text)
            {
                SetStatus("两次输入的密码不一致。");
                return;
            }

            SetStatus(operation == AccountAuthenticationOperation.Register
                ? "正在注册……"
                : "正在登录……");
            if (operation == AccountAuthenticationOperation.Register)
                _authenticator.Register(_userName.text, _password.text);
            else
                _authenticator.Login(_userName.text, _password.text);
        }

        private void SetMode(LoginMode mode)
        {
            _mode = mode;
            bool registering = mode == LoginMode.Register;
            _passwordConfirmationGroup.SetActive(registering);
            _loginTabSelected.SetActive(!registering);
            _registerTabSelected.SetActive(registering);
            _submitLabel.text = registering ? "注册" : "登录";
            if (_title != null)
                _title.text = registering ? "创建账户" : "账户登录";
            if (!registering)
                _passwordConfirmation.text = string.Empty;
            SetStatus(string.Empty);
            RefreshControls();
        }

        private void Back()
        {
            _authenticator?.CancelPendingAuthentication();
            if (NetworkManager.singleton is NetworkRoom networkRoom)
                networkRoom.HandleClientLogout();
            else
            {
                UIManager.Instance.PopPanel<LoginPanel>();
            }
        }

        private void ResolveAuthenticator()
        {
            if (_authenticator == null && NetworkManager.singleton != null)
                _authenticator = NetworkManager.singleton.GetComponent<AccountAuthenticator>();
            if (_authenticator != null && !_subscribed)
            {
                _authenticator.AuthenticationResponseReceived += OnAuthenticationResponse;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_authenticator == null || !_subscribed)
                return;
            _authenticator.AuthenticationResponseReceived -= OnAuthenticationResponse;
            _subscribed = false;
        }

        private void OnAuthenticationResponse(AccountAuthenticationResponse response)
        {
            _password.text = string.Empty;
            if (response.Code == AccountAuthenticationCode.Success)
                return;

            SetStatus(response.Code switch
            {
                AccountAuthenticationCode.InvalidUserName => "用户名格式无效。",
                AccountAuthenticationCode.InvalidPassword => "密码长度必须为 8 到 128 个字符。",
                AccountAuthenticationCode.UserNameTaken => "该用户名已经注册。",
                AccountAuthenticationCode.InvalidCredentials => "用户名或密码错误。",
                AccountAuthenticationCode.SessionExpired => "保存的登录状态已失效，请重新输入密码。",
                AccountAuthenticationCode.StorageUnavailable => "账户数据库暂时不可用，请稍后重试。",
                _ => "未知的账户认证错误。",
            });
        }

        private void SetStatus(string message)
        {
            if (_status != null)
                _status.text = message;
        }
    }
}
