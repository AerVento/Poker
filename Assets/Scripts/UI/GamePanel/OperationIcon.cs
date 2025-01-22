using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.GamePanelElement
{
    public class OperationIcon : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _chipsText;

        private uint _chips;
        public uint Chips
        {
            get => _chips;
            set
            {
                _chips = value;
                if(_chipsText != null)
                    _chipsText.text = "$" + BigNumberToString(value);
            }
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
