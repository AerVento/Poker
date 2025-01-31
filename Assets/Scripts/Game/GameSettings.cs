using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class GameSettings
    {
        private string _nickname;
        public string Nickname
        {
            get => _nickname;
            set
            {
                _nickname = value;
                PlayerPrefs.SetString("nickname", value);
            }
        }
        public GameSettings()
        {
            System.Random r = new System.Random();
            _nickname = PlayerPrefs.GetString("nickname", defaultValue:$"Anonymous{r.Next(0,10000)}");
        }
    }
}
