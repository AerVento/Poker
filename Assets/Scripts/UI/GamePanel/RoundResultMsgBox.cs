using Framework.UI;
using Game.Network;
using Mirror;
using System.Collections.Generic;
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
        private TextMeshProUGUI _confirmButtonLabel;

        [SerializeField]
        private TextMeshProUGUI _readyStatus;

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
        private NetworkRoom _network;
        private bool _automatic;
        
        public RoundResult Info
        {
            get => _info;
            set
            {
                SetResult(value, false);
            }
        }

        public void SetResult(RoundResult result, bool automatic)
        {
            bool sameHand = _info != null && result != null && _info.HandId == result.HandId;
            if (!sameHand || automatic)
                _automatic = automatic;
            _info = result;
            if (automatic) GamePanel.Instance?.HoldRoundBalances(result);
            if (_info != null)
                Refresh();
            UpdateReadyState();
        }

        public void Refresh()
        {
            if (_info == null)
                return;
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

            // BestHand is computed by the authoritative server from only cards that are public.
            var entries = _info.PlayerResults.Where(player => !player.IsFold).Select(player =>
            {
                bool showPattern = player.BestHand != null && player.BestHand.Count == 5 && player.BestRank.HasValue;
                var pattern = showPattern ? new PokerPattern
                {
                    Rank = player.BestRank.Value,
                    Grades = player.BestGrades,
                } : default;
                return (player, showPattern, pattern);
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
                    component.SetPattern(entry.pattern, entry.player.BestHand.ToArray());
                component.ChipEarned = entry.player.ChipEarned;
                component.SetRevealAvailable(CanReveal(entry.player.PlayerName), RequestReveal);
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
            _network = NetworkManager.singleton as NetworkRoom;
            if (_network != null)
            {
                _network.OnRoundReadyChanged += OnReadyChanged;
                _network.OnRoundResultChanged += OnResultChanged;
            }
            _closeButton.onClick.AddListener(CloseOnly);
            _confirmButton.onClick.AddListener(Confirm);
            UpdateReadyState();
        }

        private void OnDisable()
        {
            _closeButton.onClick.RemoveListener(CloseOnly);
            _confirmButton.onClick.RemoveListener(Confirm);
            if (_network != null)
            {
                _network.OnRoundReadyChanged -= OnReadyChanged;
                _network.OnRoundResultChanged -= OnResultChanged;
            }
            _network = null;
            if (_automatic) GamePanel.Instance?.ReleaseRoundBalances(_info);
            _automatic = false;
        }

        private void CloseOnly() => Hide();

        private void Confirm()
        {
            var ready = _network != null ? _network.LastRoundReady : default;
            if (_automatic && _info != null && ready.HandId == _info.HandId &&
                ready.RequiresConfirmation && !ready.IsConfirmed)
            {
                if (!_network.RequestRoundReady(_info.HandId))
                    return;
            }
            Hide();
        }

        private void OnReadyChanged(RoundReadySync _) => UpdateReadyState();

        private void OnResultChanged(RoundResult result, bool automatic)
        {
            if (result == null)
                return;
            if (_info != null && _info.HandId == result.HandId)
                SetResult(result, automatic);
        }

        private void UpdateReadyState()
        {
            var ready = _network != null ? _network.LastRoundReady : default;
            bool participant = _automatic && _info != null && ready.HandId == _info.HandId &&
                ready.RequiresConfirmation;
            if (_confirmButtonLabel != null)
                _confirmButtonLabel.text = participant ? ready.IsConfirmed ? "已确认" : "确认并准备" :
                    _automatic ? "确认" : "关闭";
            if (_confirmButton != null)
                _confirmButton.interactable = !participant || !ready.IsConfirmed;
            if (_readyStatus != null)
            {
                _readyStatus.gameObject.SetActive(participant);
                _readyStatus.text = participant ? $"已确认 {ready.ConfirmedCount} / {ready.RequiredCount}" : string.Empty;
            }

            if (_info != null)
            {
                foreach (var item in _listItems)
                    item.SetRevealAvailable(CanReveal(item.PlayerName), RequestReveal);
            }
        }

        private bool CanReveal(string playerName)
        {
            if (_network == null || _info == null || _info.IsRevealed ||
                _network.LastRoundReady.HandId != _info.HandId || !_network.LastRoundReady.CanReveal)
                return false;
            return _info.RevealPlayerName == playerName && GameController.Available &&
                GameController.Instance.State.Nickname == playerName;
        }

        private void RequestReveal(string playerName)
        {
            if (CanReveal(playerName))
                _network.RequestRevealLastHand(_info.HandId);
        }


    }

}
