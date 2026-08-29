using Framework.UI;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class TitlePanel : SingletonPanel<TitlePanel>
    {
        [SerializeField]
        private TextMeshProUGUI _onlineStatus;
        [SerializeField]
        private Button _start;
        [SerializeField] 
        private Button _option;
        [SerializeField]
        private Button _quit;

        [Header("Account Authentication (optional until the prefab is updated)")]
        [SerializeField]
        private TMP_InputField _userName;
        [SerializeField]
        private TMP_InputField _password;
        [SerializeField]
        private Button _register;
        [SerializeField]
        private TextMeshProUGUI _authenticationStatus;

        private AccountAuthenticator _accountAuthenticator;
        private bool _authenticationResponseSubscribed;

        private void OnEnable()
        {
            _start.onClick.AddListener(Play);
            _option.onClick.AddListener(Option);
            _quit.onClick.AddListener(Quit);
            _register?.onClick.AddListener(Register);

            if (_password != null)
            {
                _password.contentType = TMP_InputField.ContentType.Password;
                _password.ForceLabelUpdate();
            }

            ResolveAuthenticator();
        }

        private void Update()
        {
            ResolveAuthenticator();
            var isAuthenticated = NetworkClient.connection != null &&
                NetworkClient.connection.isAuthenticated;
            if (isAuthenticated)
            {
                _onlineStatus.color = Color.green;
                _onlineStatus.text = "已登录";
            }
            else if (NetworkClient.isConnected)
            {
                _onlineStatus.color = Color.yellow;
                _onlineStatus.text = "正在认证";
            }
            else if (NetworkClient.active)
            {
                _onlineStatus.color = Color.yellow;
                _onlineStatus.text = "正在连接";
            }
            else
            {
                _onlineStatus.color = Color.yellow;
                _onlineStatus.text = "离线";
            }

            var pending = _accountAuthenticator != null &&
                _accountAuthenticator.IsAuthenticationPending;
            _start.interactable = !pending;
            if (_register != null)
                _register.interactable = !pending;
            if (_authenticationStatus != null)
                _authenticationStatus.text = pending ? "正在登录服务器……" : string.Empty;
        }

        private void OnDisable()
        {
            _start.onClick.RemoveListener(Play);
            _option.onClick.RemoveListener(Option);
            _quit.onClick.RemoveListener(Quit);
            _register?.onClick.RemoveListener(Register);

            if (_accountAuthenticator != null && _authenticationResponseSubscribed)
            {
                _accountAuthenticator.AuthenticationResponseReceived -= OnAuthenticationResponse;
                _authenticationResponseSubscribed = false;
            }
        }

        private void Play()
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
            if (_accountAuthenticator == null || _userName == null || _password == null)
            {
                MsgBox.Create((msgbox) =>
                {
                    msgbox.Title = "登录界面尚未绑定";
                    msgbox.Message = "请在 TitlePanel 预制体中绑定用户名、密码和注册按钮后再登录。";
                });
                return;
            }

            if (string.IsNullOrEmpty(_userName.text) || string.IsNullOrEmpty(_password.text))
            {
                MsgBox.Create((msgbox) =>
                {
                    msgbox.Title = "无法提交";
                    msgbox.Message = "用户名和密码不能为空。";
                });
                return;
            }

            if (operation == AccountAuthenticationOperation.Register)
                _accountAuthenticator.Register(_userName.text, _password.text);
            else
                _accountAuthenticator.Login(_userName.text, _password.text);
        }

        private void ResolveAuthenticator()
        {
            if (_accountAuthenticator == null && NetworkManager.singleton != null)
                _accountAuthenticator = NetworkManager.singleton.GetComponent<AccountAuthenticator>();

            if (_accountAuthenticator != null && !_authenticationResponseSubscribed)
            {
                _accountAuthenticator.AuthenticationResponseReceived += OnAuthenticationResponse;
                _authenticationResponseSubscribed = true;
            }
        }

        private void OnAuthenticationResponse(AccountAuthenticationResponse response)
        {
            if (_password != null)
                _password.text = string.Empty;
            if (response.Code == AccountAuthenticationCode.Success)
                return;

            var message = response.Code switch
            {
                AccountAuthenticationCode.InvalidUserName => "用户名格式无效。",
                AccountAuthenticationCode.InvalidPassword => "密码长度必须为 8 到 128 个字符。",
                AccountAuthenticationCode.UserNameTaken => "该用户名已经被注册。",
                AccountAuthenticationCode.InvalidCredentials => "用户名或密码错误。",
                AccountAuthenticationCode.StorageUnavailable => "账户数据库暂时不可用，请稍后重试。",
                _ => "未知的账户认证错误。",
            };
            MsgBox.Create((msgbox) =>
            {
                msgbox.Title = response.Operation == AccountAuthenticationOperation.Register
                    ? "注册失败"
                    : "登录失败";
                msgbox.Message = message;
            });
        }

        private void Option()
        {
            UIManager.Instance.HidePanel<TitlePanel>();
            UIManager.Instance.ShowPanel<OptionPanel>();
        }

        private void Quit()
        {
            Application.Quit();
        }
    }

}
