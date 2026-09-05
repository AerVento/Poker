
using Framework.Configs;
using Framework.Log;
using Framework.Singleton;
using Framework.UI;
using Game.Network.Diagnostics;
using Game.Network;
using Cysharp.Threading.Tasks;
using Game.UI;
using Mirror;
using Mirror.SimpleWeb;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


namespace Game
{
    public class GameController : MonoSingleton<GameController>
    {
        public GameSettings State { get; private set; }
        public bool HasNetworkInitializationStarted { get; private set; }
        public bool IsNetworkInitializing { get; private set; }
        public bool IsClientNetworkReady { get; private set; }
        
        protected override void Awake()
        {
            base.Awake();

            DontDestroyOnLoad(gameObject);
            State = new GameSettings(); // PlayerPrefs不能在构造函数中调用
        }

        private void Start()
        {
            if (PokerIntegrationRunner.TryStart(this))
                return;

            if (NetworkConfigurationLoader.IsServerProcess)
            {
                StartConfiguredServer();
                return;
            }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_WEBGL
            UIManager.Instance.PushPanel<TitlePanel>();
            Application.targetFrameRate = 60;
            AdjustScreen();
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception e = args.ExceptionObject as Exception;
                Framework.Log.Log.WriteError($"Unhandled exception: {e?.Message}\n{e?.StackTrace}");
                Framework.Log.Log.Close();
            };
#endif

            RetryClientNetworkInitialization();
        }

        public void RetryClientNetworkInitialization()
        {
            if (IsNetworkInitializing || IsClientNetworkReady)
                return;
            InitializeClientNetworkAsync().Forget();
        }

        private async UniTask InitializeClientNetworkAsync()
        {
            HasNetworkInitializationStarted = true;
            IsNetworkInitializing = true;
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            try
            {
                if (UIManager.Available)
                    UIManager.Instance.PopPanel("NetworkConfigurationError");
                var configuration = await NetworkConfigurationLoader.LoadClientAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!(NetworkManager.singleton is NetworkRoom networkRoom))
                    throw new InvalidOperationException("NetworkRoom is missing from the startup scene.");
                networkRoom.ConfigureClient(configuration);
                IsClientNetworkReady = true;
                NetworkConfigurationLoader.LogInfo($"Client network configuration loaded: {configuration.ServerUri}.");
                networkRoom.StartSilentClientConnection(configuration.ConnectTimeoutSeconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The game was closed while its deployment configuration was downloading.
            }
            catch (Exception exception)
            {
                IsClientNetworkReady = false;
                NetworkConfigurationLoader.LogError($"Client network initialization failed: {exception.Message}");
                if (UIManager.Available)
                {
                    MsgBox.Create("NetworkConfigurationError", box =>
                    {
                        box.Title = "无法连接服务";
                        box.Message = "暂时无法连接服务，请稍后重试。";
                        box.Type = MsgboxType.ConfirmAndCancel;
                        box.ConfirmButtonText = "重试";
                        box.CancelButtonText = "稍后";
                        box.ShowCloseButton = false;
                        box.OnConfirm += RetryClientNetworkInitialization;
                    });
                }
            }
            finally
            {
                IsNetworkInitializing = false;
            }
        }

        private static void StartConfiguredServer()
        {
            try
            {
                if (!(NetworkManager.singleton is NetworkRoom networkRoom))
                    throw new InvalidOperationException("NetworkRoom is missing from the startup scene.");
                NetworkConfigurationLoader.LoadAndApplyServer(networkRoom);
                networkRoom.StartServer();
                if (!NetworkServer.active || !networkRoom.transport.ServerActive())
                    throw new InvalidOperationException("The game server did not start listening.");
            }
            catch (Exception exception)
            {
                NetworkConfigurationLoader.LogError($"Server startup failed: {exception.Message}");
                if (NetworkServer.active && NetworkManager.singleton != null)
                    NetworkManager.singleton.StopServer();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit(1);
#endif
            }
        }

        private void AdjustScreen()
        {
            int width = PlayerPrefs.GetInt("Screen_Width", Screen.width);
            int height = PlayerPrefs.GetInt("Screen_Height", Screen.height);
            bool fullScreen = PlayerPrefs.HasKey("Screen_Fullscreen") ? 
                PlayerPrefs.GetInt("Screen_Fullscreen") == 1 : 
                Screen.fullScreen;
            Screen.SetResolution(width, height, fullScreen);
        }
        
        private void OnApplicationQuit()
        {
            Framework.Log.Log.Close();
        }
    }
}

