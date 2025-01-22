using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.RoomPanelElement
{
    public class PlayerListItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _playername;

        [SerializeField]
        private GameObject _ownerIcon;

        [SerializeField]
        private Image _background;
        [SerializeField]
        private Sprite _unreadySprite;
        [SerializeField]
        private Sprite _readySprite;

        private string _name;
        private bool _isOwner;
        private bool _isReady;
        public string PlayerName
        {
            get => _name; 
            set
            {
                _name = value;
                _playername.text = _name;
            }
        } 
        public bool IsOwner
        {
            get => _isOwner;
            set
            {
                _isOwner = value;
                _ownerIcon.SetActive(value);
            }
        }
        public bool IsReady
        {
            get => _isReady;
            set
            {
                _isReady = value;
                if (value)
                {
                    _background.sprite = _readySprite;
                }
                else
                {
                    _background.sprite = _unreadySprite;
                }
            }
        }
    }
}
