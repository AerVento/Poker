using Cysharp.Threading.Tasks;
using Framework.SO;
using Game.SO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class PokerElement : MonoBehaviour
    {
        [SerializeField]
        private Image _image;

        private Poker _poker;
        private bool _isShowing = true;

        private PokerCardsSO GetSO() => SingletonSOManager.Instance.GetSOFile<PokerCardsSO>();

        /// <summary>
        /// �˿�
        /// </summary>
        public Poker Poker
        {
            get => _poker;
            set
            {
                _poker = value;
                if (_isShowing)
                {
                    _image.sprite = GetSO().GetSprite(_poker);
                }
            }
        }

        /// <summary>
        /// �Ƿ�ɼ������ɼ���ֻ����ʾ����
        /// </summary>
        public bool IsShowing
        {
            get => _isShowing;
            set
            {
                _isShowing = value;
                if (value)
                    _image.sprite = GetSO().GetSprite(_poker);
                else
                    _image.sprite = GetSO().Back;

            }
        }
    }
}
