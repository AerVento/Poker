using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class PlayerListItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _playername;

        [SerializeField]
        private GameObject _ownerIcon;

        private string _name;
        private bool _isOwner;
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
    }
}
