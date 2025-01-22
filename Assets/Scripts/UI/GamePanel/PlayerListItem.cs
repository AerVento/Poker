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
                _chipsText.text = "$" + BigNumberToString(value);
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
                _isOperating = value;
                _timeText.gameObject.SetActive(value);
                _background.sprite = value ? _opertingSprite : _nonOperatingSprite;

                if (value && !_timer.IsCounting)
                    _timer.Start();
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
                _canvasGroup.alpha = value ? 0.4f : 1;
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
                _checkIcon.gameObject.SetActive(false);
                _betIcon.gameObject.SetActive(false);
                _raiseIcon.gameObject.SetActive(false);
                _callIcon.gameObject.SetActive(false);
                _allInIcon.gameObject.SetActive(false);
                _foldIcon.gameObject.SetActive(false);
                

                if (value != null)
                {
                    OperationIcon icon = value.Type switch
                    {
                        PlayerOperationType.Check => _checkIcon,
                        PlayerOperationType.Bet => _betIcon,
                        PlayerOperationType.Raise => _raiseIcon,
                        PlayerOperationType.Call => _callIcon,
                        PlayerOperationType.AllIn => _allInIcon,
                        PlayerOperationType.Fold => _foldIcon,
                        _ => throw new System.IndexOutOfRangeException()
                    };
                    icon.gameObject.SetActive(true);
                    icon.Chips = value.Number;
                }
            }
        }

        public void StartCountDown() => _timer.Start();

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

            // 设置时间
            _timeText.text = _timer.IsCounting ? System.Convert.ToInt32(System.Math.Ceiling(_timer.TimeRemaining)).ToString() : "0";
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
