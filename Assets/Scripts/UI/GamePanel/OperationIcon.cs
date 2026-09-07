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
                    _chipsText.text = "$" + ChipAmountFormatter.Format(value);
            }
        }


    }
}
