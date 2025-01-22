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
                _chipsText.text = "$" + BigNumberToString(value);
            }
        }

        public bool IsDisconnected
        {
            get => _disconnectedIcon.gameObject.activeSelf;
            set => _disconnectedIcon.gameObject.SetActive(value);
        }

        private string BigNumberToString(uint number)
        {
            if (number < 1000)
                return number.ToString();
            else if (number < 1000000)
                return System.MathF.Round((float)number / 1000, 1).ToString() + "K";
            else if (number < 1000000000)
                return System.MathF.Round((float)number / 1000000, 1).ToString() + "M";
            else
                return System.MathF.Round((float)number / 1000000000, 1).ToString() + "B";
        }
    }
}
