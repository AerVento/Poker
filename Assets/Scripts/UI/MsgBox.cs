using Framework.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public enum MsgType
    {
        Confirm,
        Cancel,
        ConfirmAndCancel,
    }
    public class MsgBox : BasePanel
    {
        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        private Button _confirmButton;

        [SerializeField]
        private Button _cancelButton;

        [SerializeField]
        private TextMeshProUGUI _title;

        [SerializeField]
        private TextMeshProUGUI _message;

        private MsgType _type = MsgType.Confirm;
        private bool _showCloseButton = true;


        public event System.Action OnConfirm;
        public event System.Action OnCancel;

        public MsgType Type
        {
            get => _type;
            set
            {
                _type = value;
                switch (value)
                {
                    case MsgType.Confirm:
                        _confirmButton.gameObject.SetActive(true);
                        _cancelButton.gameObject.SetActive(false);
                        break;
                    case MsgType.Cancel:
                        _confirmButton.gameObject.SetActive(false);
                        _cancelButton.gameObject.SetActive(true);
                        break;
                    case MsgType.ConfirmAndCancel:
                        _confirmButton.gameObject.SetActive(true);
                        _cancelButton.gameObject.SetActive(true);
                        break;
                }
            }
        }

        /// <summary>
        /// 是否展示左上角X按钮
        /// </summary>
        public bool ShowCloseButton
        {
            get => _showCloseButton;
            set
            {
                _showCloseButton = value;
                _closeButton.gameObject.SetActive(value);
            }
        }

        public string Title
        {
            get => _title.text;
            set => _title.text = value;
        }

        public string Message
        {
            get => _message.text;
            set => _message.text = value;
        }

        private void Start()
        {
            _closeButton.onClick.AddListener(Hide);
            _confirmButton.onClick.AddListener(() =>
            {
                OnConfirm?.Invoke();
                Hide();
            });
            _cancelButton.onClick.AddListener(() =>
            {
                OnCancel?.Invoke();
                Hide();
            });
        }

        public override void Hide()
        {
            _closeButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.RemoveAllListeners();
            OnConfirm = null;
            OnCancel = null;
            UIManager.Instance.DestroyPanel(Identifier);
        }


        /// <summary>
        /// 打开一个MsgBox，使用给定的identifier
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public static MsgBox Create(string identifier)
        {
            return UIManager.Instance.CreatePanel<MsgBox>(identifier, UIManager.Layer.System);
        }

        /// <summary>
        /// 打开一个MsgBox，使用给定的identifier，通过回调对其进行设置。
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="setting"></param>
        public static void Create(string identifier, System.Action<MsgBox> setting = null)
        {
            setting?.Invoke(Create(identifier));
        }

        /// <summary>
        /// 打开一个MsgBox，使用随机生成的Identifier
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public static MsgBox Create()
        {
            return UIManager.Instance.CreatePanel<MsgBox>(System.Guid.NewGuid().ToString(), UIManager.Layer.System);
        }

        /// <summary>
        /// 打开一个MsgBox，使用随机生成的Identifier，通过回调对其进行设置。
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="setting"></param>
        public static void Create(System.Action<MsgBox> setting = null)
        {
            setting?.Invoke(Create());
        }
    }

}
