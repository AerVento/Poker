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
        [SerializeField] private NamecardAppearance _namecard;
        public void SetNamecard(string id, ulong serviceYears = 0) => _namecard?.Present(id, serviceYears, _playername);

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
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private GameObject _spectatorIcon;
        [SerializeField, Range(0f, 1f)] private float _spectatorAlpha = 0.4f;

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
                IsSpectator = !string.IsNullOrEmpty(value) && value.Contains("旁观");
            }
        }

        public bool IsSpectator
        {
            set
            {
                if (_spectatorIcon != null)
                    _spectatorIcon.SetActive(value);
                if (_canvasGroup != null)
                    _canvasGroup.alpha = value ? _spectatorAlpha : 1f;
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
                _namecard?.SetHighlighted(value);
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
