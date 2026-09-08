using Framework.Timer;
using Game.Network;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.GamePanelElement
{
    public class PlayerListItem : MonoBehaviour
    {
        [SerializeField] private NamecardAppearance _namecard;
        public void SetNamecard(string id, ulong serviceYears = 0) => _namecard?.Present(id, serviceYears, _name);
        public void SetNamecard(Game.SO.NamecardProductSO product) => SetNamecard(product != null ? product.Id : null);

        [Tooltip("Spacing between instances of this layout prefab.")]
        [SerializeField] private Vector2 _listSpacing = new Vector2(14, 14);
        public Vector2 ListSpacing => _listSpacing;
        public double CountdownRemaining => _timer.TimeRemaining;

        // Rebuilding the view must not give the acting player a fresh countdown.
        public void RestoreCountdown(double remaining)
        {
            if (!_isOperating)
                return;
            _timer.Stop();
            if (remaining > 0)
            {
                _timer.Duration = System.Math.Min(remaining, _thinkingTime);
                _timer.Start();
            }
        }

        [Header("GameObjects")]
        [SerializeField]
        private GameObject _disconnected;

        [SerializeField]
        private GameObject _bigBlind;

        [SerializeField]
        private GameObject _smallBlind;

        [SerializeField]
        private GameObject _dealer;

        [Header("Operation Icons")]
        [SerializeField]
        private OperationIcon _checkIcon;
        [SerializeField]
        private OperationIcon _betIcon;
        [SerializeField]
        private OperationIcon _raiseIcon;
        [SerializeField]
        private OperationIcon _callIcon;
        [SerializeField]
        private OperationIcon _allInIcon;
        [SerializeField]
        private OperationIcon _foldIcon;

        [Header("UI Texts")]
        [SerializeField]
        private TextMeshProUGUI _timeText;

        [SerializeField]
        private TextMeshProUGUI _name;

        [SerializeField]
        private TextMeshProUGUI _chipsText;

        [Header("Images")]
        [SerializeField]
        private Image _background;

        [SerializeField]
        private Sprite _opertingSprite;

        [SerializeField]
        private Sprite _nonOperatingSprite;

        [SerializeField]
        private CanvasGroup _canvasGroup;

        [Range(0, 1)]
        [SerializeField] private float _foldedAlpha = 0.4f;

        private uint _chips;

        private float _thinkingTime = 30;

        private Timer _timer = new Timer(30);

        private bool _isOperating;

        private bool _isFold;

        private PlayerOperation _operation;

        public string PlayerName
        {
            get => _name.text;
            set => _name.text = value;
        }

        public uint Chip
        {
            get => _chips;
            set
            {
                _chips = value;
                _chipsText.text = "$" + ChipAmountFormatter.Format(value);
            }
        }

        /// <summary>
        /// 思考时间（秒）
        /// </summary>
        public float ThinkingTime
        {
            get => _thinkingTime;
            set
            {
                if (_thinkingTime == value || value <= 0 || float.IsNaN(value) || float.IsInfinity(value))
                    return;
                _thinkingTime = value;
                if(_timer.IsCounting)
                {
                    _timer.Stop();
                    _timer.Duration = value;
                    _timer.Start();
                }
                else
                    _timer.Duration = value;
            }
        }

        public bool IsBigBlind
        {
            get => _bigBlind.gameObject.activeSelf;
            set => _bigBlind.gameObject.SetActive(value);
        }

        public bool IsSmallBlind
        {
            get => _smallBlind.gameObject.activeSelf;
            set => _smallBlind.gameObject.SetActive(value);
        }

        public bool IsDealer
        {
            get => _dealer.gameObject.activeSelf;
            set => _dealer.gameObject.SetActive(value);
        }

        /// <summary>
        /// 是否正在操作
        /// </summary>
        public bool IsOperating
        {
            get => _isOperating;
            set
            {
                bool changed = _isOperating != value;
                _isOperating = value;
                RefreshStatus();
                _namecard?.SetHighlighted(value);
                _background.sprite = value ? _opertingSprite : _nonOperatingSprite;
                if (!changed)
                    return;

                if (value && !_timer.IsCounting)
                {
                    _timer.Duration = _thinkingTime;
                    _timer.Start();
                }
                else if (!value && _timer.IsCounting)
                    _timer.Stop();
            }
        }

        public bool IsFold
        {
            get => _isFold;
            set
            {
                _isFold = value;
                _canvasGroup.alpha = value ? _foldedAlpha : 1;
            }
        }

        public bool IsDisconnected
        {
            get => _disconnected.gameObject.activeSelf;
            set => _disconnected.gameObject.SetActive(value);
        }

        public PlayerOperation Operation
        {
            get => _operation;
            set
            {
                _operation = value;
                RefreshStatus();
            }
        }

        // A player may retain their previous action when it becomes their turn again.
        // The shared status slot shows the countdown until that turn has ended.
        private void RefreshStatus()
        {
            _timeText.gameObject.SetActive(_isOperating);
            _checkIcon.gameObject.SetActive(false);
            _betIcon.gameObject.SetActive(false);
            _raiseIcon.gameObject.SetActive(false);
            _callIcon.gameObject.SetActive(false);
            _allInIcon.gameObject.SetActive(false);
            _foldIcon.gameObject.SetActive(false);

            if (_isOperating || _operation == null)
                return;

            OperationIcon icon = _operation.Type switch
            {
                PlayerOperationType.Check => _checkIcon,
                PlayerOperationType.Bet => _betIcon,
                PlayerOperationType.Raise => _raiseIcon,
                PlayerOperationType.Call => _callIcon,
                PlayerOperationType.AllIn => _allInIcon,
                PlayerOperationType.Fold => _foldIcon,
                _ => throw new System.IndexOutOfRangeException()
            };
            icon.Chips = _operation.Number;
            icon.gameObject.SetActive(true);
        }

        public void StartCountDown() => _timer.Start();

        private void OnDisable()
        {
            _timer.Stop();
            _isOperating = false;
            _namecard?.SetHighlighted(false);
            RefreshStatus();
            _background.sprite = _nonOperatingSprite;
        }

        // Update is called once per frame
        void Update()
        {

            // 设置时间
            var seconds = _timer.IsCounting ? System.Convert.ToInt32(System.Math.Ceiling(_timer.TimeRemaining)) : 0;
            _timeText.text = $"思考 {seconds}s";
        }


    }

}
