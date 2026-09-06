using Framework.UI;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class LoginPanel : SingletonPanel<LoginPanel>
    {
        [Header("Inputs")]
        [SerializeField]
        private TMP_InputField _userName;
        [SerializeField]
        private TMP_InputField _password;

        [Header("Buttons")]
        [SerializeField]
        private Button _login;
        [SerializeField]
        private Button _register;
        [SerializeField]
        private Button _back;

        [Header("Labels")]
        [SerializeField]
        private TextMeshProUGUI _status;

        private AccountAuthenticator _authenticator;
        private bool _subscribed;
        private bool _savedLoginStarted;

        internal bool OpenTasksAfterLogin { get; private set; }

        private void OnEnable()
        {
            _login.onClick.AddListener(Login);
            _register.onClick.AddListener(Register);
            _back.onClick.AddListener(Back);
            _password.contentType = TMP_InputField.ContentType.Password;
            _password.ForceLabelUpdate();
            _userName.text = LoginCredentialStore.SavedUserName;
            SetStatus(string.Empty);
            ResolveAuthenticator();
            RefreshControls();
        }

        private void Update() => RefreshControls();

        private void RefreshControls()
        {
            ResolveAuthenticator();
            var pending = _authenticator != null && _authenticator.IsAuthenticationPending;
            _login.interactable = !pending;
            _register.interactable = !pending;
            _back.interactable = true;
            _userName.interactable = !pending;
            _password.interactable = !pending;
        }

        private void OnDisable()
        {
            _login.onClick.RemoveListener(Login);
            _register.onClick.RemoveListener(Register);
            _back.onClick.RemoveListener(Back);
            Unsubscribe();
            _savedLoginStarted = false;
            OpenTasksAfterLogin = false;
            _password.text = string.Empty;
        }

        public void BeginTaskLogin()
        {
            OpenTasksAfterLogin = true;
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

        private void Login()
        {
            Authenticate(AccountAuthenticationOperation.Login);
        }

        private void Register()
        {
            Authenticate(AccountAuthenticationOperation.Register);
        }

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

            SetStatus(operation == AccountAuthenticationOperation.Register
                ? "正在注册……"
                : "正在登录……");
            if (operation == AccountAuthenticationOperation.Register)
                _authenticator.Register(_userName.text, _password.text);
            else
                _authenticator.Login(_userName.text, _password.text);
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
