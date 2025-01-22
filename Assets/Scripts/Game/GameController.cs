
using Framework.Configs;
using Framework.Log;
using Framework.Singleton;
using Framework.UI;
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



#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            //NetworkManager.singleton.AddComponent<NetworkManagerHUD>();
            //NetworkManager.singleton.networkAddress = "localhost";
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

