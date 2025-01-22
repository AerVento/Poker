using Framework.UI;
using Game.Network;
using Mirror.Examples.BilliardsPredicted;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.GamePanelElement
{
    public class RoundResultMsgBox : SingletonPanel<RoundResultMsgBox>
    {
        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        private Button _confirmButton;

        [SerializeField]
        private List<PokerElement> _community;

        [SerializeField]
        private GameObject _roundResultListItemPrefab;

        [SerializeField]
        private Transform _listTransform;

        private List<RoundResultListItem> _listItems = new List<RoundResultListItem>();

        private RoundResult _info;
        
        public RoundResult Info
        {
            get => _info;
            set
            {
                _info = value;
                Refresh();
            }
        }

        public void Refresh()
        {
            foreach (var elem in _community)
            {
                elem.IsShowing = false;
            }

            for (int i = 0; i < _info.Community.Count; i++)
            {
                var elem = _community[i];
                elem.Poker = _info.Community[i];
                elem.IsShowing = true;
            }

            foreach (var tmp in _listItems)
            {
                Destroy(tmp.gameObject);
            }
            _listItems.Clear();

            if(_info.Community.Count == 5)
            {
                List<(PokerPattern pattern, Poker[] hand, RoundResult.Player player)> list = new();
                foreach (var playerInfo in _info.PlayerResults)
                {
                    var pattern = PokerUtility.GetBiggestPattern(_info.Community.Concat(playerInfo.Hole).ToList(), out var hand);
                    list.Add((pattern, hand, playerInfo));
                }
                list.Sort((a, b) =>
                {
                    if (a.pattern.SmallerThan(b.pattern))
                        return 1;
                    else
                        return -1;
                });

                foreach (var (pattern, hand, playerInfo) in list)
                {
                    GameObject obj = Instantiate(_roundResultListItemPrefab, _listTransform);
                    var component = obj.GetComponent<RoundResultListItem>();

                    component.PlayerName = playerInfo.PlayerName;
                    component.SetHole(playerInfo.Hole);
                    component.IsFold = playerInfo.IsFold;
                    component.IsDisconnected = playerInfo.IsDisconnected;
                    component.ShowPattern = true;
                    component.SetPattern(pattern, hand);
                    component.ChipEarned = playerInfo.ChipEarned;
                    _listItems.Add(component);
                }
            }
            else
            {
                foreach (var playerInfo in _info.PlayerResults)
                {
                    GameObject obj = Instantiate(_roundResultListItemPrefab, _listTransform);
                    var component = obj.GetComponent<RoundResultListItem>();

                    component.PlayerName = playerInfo.PlayerName;
                    component.SetHole(playerInfo.Hole);
                    component.IsFold = playerInfo.IsFold;
                    component.IsDisconnected = playerInfo.IsDisconnected;
                    component.ShowPattern = false;
                    component.ChipEarned = playerInfo.ChipEarned;
                    _listItems.Add(component);
                }
            }

        }

        private void OnEnable()
        {
            _closeButton.onClick.AddListener(Hide);
            _confirmButton.onClick.AddListener(Hide);
        }

        private void OnDisable()
        {
            _closeButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.RemoveAllListeners();
        }


    }

}
