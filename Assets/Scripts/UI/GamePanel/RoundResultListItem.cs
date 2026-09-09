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
                _chipEarnedText.text = (value > 0 ? "+$" : "$") + ChipAmountFormatter.Format(value);
                _chipEarnedText.color = value > 0 ? new Color(0.97f, 0.79f, 0.41f) : new Color(0.58f, 0.66f, 0.61f);
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
            var sorted = hand.OrderByDescending(card => card.Rank);
            _patternText.text = pattern + "\n[" + string.Join(", ", sorted.Select(card => card.ToString())) + "]";
        }


    }
}
