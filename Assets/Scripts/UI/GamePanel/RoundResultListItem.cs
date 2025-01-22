using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Game.UI.GamePanelElement
{
    public class RoundResultListItem : MonoBehaviour
    {
        [SerializeField]
        private GameObject _disconnectedIcon;

        [SerializeField]
        private TextMeshProUGUI _playerNameText;

        [SerializeField]
        private List<PokerElement> _holes;

        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        private TextMeshProUGUI _patternText;

        [SerializeField]
        private TextMeshProUGUI _chipEarnedText;

        private bool _isFold;
        private bool _isDisconnected;

        private uint _chipEarned;

        public string PlayerName
        {
            get => _playerNameText.text;
            set => _playerNameText.text = value;
        }

        public bool IsFold
        {
            get => _isFold;
            set
            {
                _isFold = value;
                _canvasGroup.alpha = value ? 0.4f : 1;
            }
        }

        public bool IsDisconnected
        {
            get => _isDisconnected;
            set
            {
                _isDisconnected = value;
                _disconnectedIcon.gameObject.SetActive(value);
            }
        }

        public uint ChipEarned
        {
            get => _chipEarned;
            set
            {
                _chipEarned = value;
                _chipEarnedText.text = "+$" + BigNumberToString(value);
            }
        }

        public bool ShowPattern
        {
            get => _patternText.gameObject.activeSelf;
            set => _patternText.gameObject.SetActive(value);
        }

        public void SetHole(List<Poker> pokers)
        {
            foreach(var elem in _holes)
            {
                elem.IsShowing = false;
            }
            for(int i = 0; i < pokers.Count; i++)
            {
                var elem = _holes[i];
                elem.Poker = pokers[i];
                elem.IsShowing = true;
            }
        }

        public void SetPattern(PokerPattern pattern, Poker[] hand)
        {
            StringBuilder builder = new StringBuilder(pattern.ToString());
            List<Poker> sorted = hand.ToList(); ;
            sorted.Sort((a, b) => a.BiggerThan(b) ? -1 : 1);
            builder.Append("\n[");
            builder.Append(sorted[0].ToString());
            for(int i = 1; i < sorted.Count; i++)
            {
                builder.Append(", ");
                builder.Append(sorted[i].ToString());
            }
            builder.Append("]");
            _patternText.text = builder.ToString();
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
