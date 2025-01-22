using Framework.UI;
using Game.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.GamePanelElement
{
    public class GameResultMsgBox : SingletonPanel<GameResultMsgBox>
    {
        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        private Button _confirmButton;

        [SerializeField]
        private GameObject _gameResultListItemPrefab;

        [SerializeField]
        private Transform _listTransform;

        private List<GameResultListItem> _listItems = new List<GameResultListItem>();

        private GameResult _info;

        public GameResult Info
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
            foreach (var tmp in _listItems)
            {
                Destroy(tmp.gameObject);
            }
            _listItems.Clear();
            _info.PlayerResults.Sort((a, b) => (int)(b.Chips - a.Chips));
            foreach (var playerInfo in _info.PlayerResults)
            {
                GameObject obj = Instantiate(_gameResultListItemPrefab, _listTransform);
                var component = obj.GetComponent<GameResultListItem>();

                component.PlayerName = playerInfo.Name;
                component.Chips = playerInfo.Chips;
                component.IsDisconnected = playerInfo.IsDisconnected;

                _listItems.Add(component);
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

        public override void Hide()
        {
            base.Hide();

            UIManager.Instance.HidePanel<GamePanel>();
            UIManager.Instance.ShowPanel<TitlePanel>();
        }
    }
}
