
using Framework.Configs;
using Framework.Log;
using Framework.Singleton;
using Framework.UI;
using Game.Network.Diagnostics;
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

#if UNITY_EDITOR
            var networkManager = NetworkManager.singleton;
            if (networkManager != null)
            {
                if (networkManager.GetComponent<NetworkManagerHUD>() == null)
                    networkManager.gameObject.AddComponent<NetworkManagerHUD>();
                networkManager.networkAddress = "localhost";
            }
#endif

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            UIManager.Instance.ShowPanel<TitlePanel>();
            Application.targetFrameRate = 60;
            AdjustScreen();
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception e = args.ExceptionObject as Exception;
                Framework.Log.Log.WriteError($"Unhandled exception: {e?.Message}\n{e?.StackTrace}");
                Framework.Log.Log.Close();
            };
#endif

#if UNITY_WEBGL

            UIManager.Instance.ShowPanel<TitlePanel>();
            Application.targetFrameRate = 60;
            AdjustScreen();
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception e = args.ExceptionObject as Exception;
                Framework.Log.Log.WriteError($"Unhandled exception: {e?.Message}\n{e?.StackTrace}");
                Framework.Log.Log.Close();
            };
#endif

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_WEBGL
            if (NetworkManager.singleton is Game.Network.NetworkRoom networkRoom)
                networkRoom.StartSilentClientConnection(5f);
#endif

#if UNITY_STANDALONE_LINUX
            NetworkManager.singleton.StartServer();
#endif

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

