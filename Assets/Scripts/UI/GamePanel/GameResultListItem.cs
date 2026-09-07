using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Game.UI
{ 
    public class GameResultListItem : MonoBehaviour
    {
        [SerializeField]
        private GameObject _disconnectedIcon;
 
        [SerializeField]
        private TextMeshProUGUI _nameText;

        [SerializeField]
        private TextMeshProUGUI _chipsText;

        private uint _chips;

        public string PlayerName
        {
            get => _nameText.text;
            set => _nameText.text = value;
        }

        public uint Chips
        {
            get => _chips;
            set
            {
                _chips = value;
                _chipsText.text = "$" + ChipAmountFormatter.Format(value);
            }
        }

        public bool IsDisconnected
        {
            get => _disconnectedIcon.gameObject.activeSelf;
            set => _disconnectedIcon.gameObject.SetActive(value);
        }


    }
}
