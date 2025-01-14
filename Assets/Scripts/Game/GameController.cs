using Framework.Singleton;
using Framework.UI;
using Game.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class GameController : MonoSingleton<GameController>
    {
        public GameState State { get; private set; } = new GameState();
        
        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            UIManager.Instance.ShowPanel<TitlePanel>();
        }
    }
}

