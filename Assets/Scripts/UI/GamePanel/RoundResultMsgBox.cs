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

        [SerializeField] private RectTransform _content;
        [SerializeField] private ScrollRect _scroll;

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
                tmp.gameObject.SetActive(false);
                Destroy(tmp.gameObject);
            }
            _listItems.Clear();

            // Only rank hands that the server actually disclosed at showdown.
            var entries = _info.PlayerResults.Where(player => !player.IsFold).Select(player =>
            {
                bool showPattern = _info.Community.Count == 5 && !player.IsFold && player.Hole.Count == 2;
                Poker[] hand = null;
                var pattern = showPattern
                    ? PokerUtility.GetBiggestPattern(_info.Community.Concat(player.Hole).ToList(), out hand)
                    : default;
                return (player, showPattern, pattern, hand);
            }).ToList();
            entries.Sort((a, b) =>
            {
                if (a.showPattern != b.showPattern)
                    return b.showPattern.CompareTo(a.showPattern);
                if (!a.showPattern)
                    return b.player.ChipEarned.CompareTo(a.player.ChipEarned);
                if (a.pattern.BiggerThan(b.pattern))
                    return -1;
                return a.pattern.SmallerThan(b.pattern) ? 1 : 0;
            });

            foreach (var entry in entries)
            {
                var obj = Instantiate(_roundResultListItemPrefab, _listTransform);
                var component = obj.GetComponent<RoundResultListItem>();
                component.PlayerName = entry.player.PlayerName;
                component.SetHole(entry.player.Hole);
                component.IsFold = entry.player.IsFold;
                component.IsDisconnected = entry.player.IsDisconnected;
                component.ShowPattern = entry.showPattern;
                if (entry.showPattern)
                    component.SetPattern(entry.pattern, entry.hand);
                component.ChipEarned = entry.player.ChipEarned;
                _listItems.Add(component);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_listTransform);
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f;
        }


        private void OnRectTransformDimensionsChange() => FitContent();

        private void FitContent()
        {
            if (_content == null) return;
            var size = ((RectTransform)transform).rect.size;
            if (size.x > 0 && size.y > 0)
                _content.localScale = Vector3.one * Mathf.Min(size.x / 1800f, size.y / 1040f);
        }

        private void OnEnable()
        {
            FitContent();
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
