using Framework.Singleton;
using Framework.UI;
using Game.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

namespace Game
{
    public class GameController : MonoSingleton<GameController>
    {
        public GameState State { get; private set; }
        
        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
            State = new GameState(); // PlayerPrefs不能在构造函数中调用
        }

        private void Start()
        {
            UIManager.Instance.ShowPanel<TitlePanel>();
            AdjustScreen();
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

    }
}

