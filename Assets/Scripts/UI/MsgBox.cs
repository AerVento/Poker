using Framework.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.UI
{
    public enum MsgboxType
    {
        Confirm,
        Cancel,
        ConfirmAndCancel,
        None,
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

        private MsgboxType _type = MsgboxType.Confirm;
        private bool _showCloseButton = true;
        private bool _closed;
        private bool _preferCancel;
        private GameObject _previousSelection;


        public event System.Action OnConfirm;
        public event System.Action OnCancel;

        public MsgboxType Type
        {
            get => _type;
            set
            {
                _type = value;
                switch (value)
                {
                    case MsgboxType.Confirm:
                        _confirmButton.gameObject.SetActive(true);
                        _cancelButton.gameObject.SetActive(false);
                        break;
                    case MsgboxType.Cancel:
                        _confirmButton.gameObject.SetActive(false);
                        _cancelButton.gameObject.SetActive(true);
                        break;
                    case MsgboxType.ConfirmAndCancel:
                        _confirmButton.gameObject.SetActive(true);
                        _cancelButton.gameObject.SetActive(true);
                        break;
                    case MsgboxType.None:
                        _confirmButton.gameObject.SetActive(false);
                        _cancelButton.gameObject.SetActive(false);
                        break;
                }
                if (IsShowing)
                    SelectDefaultButton();
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
                if (IsShowing)
                    SelectDefaultButton();
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

        public string ConfirmButtonText
        {
            get => _confirmButton.GetComponentInChildren<TextMeshProUGUI>().text;
            set => _confirmButton.GetComponentInChildren<TextMeshProUGUI>().text = value;
        }

        public string CancelButtonText
        {
            get => _cancelButton.GetComponentInChildren<TextMeshProUGUI>().text;
            set => _cancelButton.GetComponentInChildren<TextMeshProUGUI>().text = value;
        }

        private void Awake()
        {
            Type = _type;
            ShowCloseButton = _showCloseButton;
        }

        private void OnEnable()
        {
            _previousSelection = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            IsShowing = true;
            SelectDefaultButton();
            _closeButton.onClick.AddListener(Cancel);
            _confirmButton.onClick.AddListener(Confirm);
            _cancelButton.onClick.AddListener(Cancel);
        }

        private void OnDisable()
        {
            _closeButton.onClick.RemoveListener(Cancel);
            _confirmButton.onClick.RemoveListener(Confirm);
            _cancelButton.onClick.RemoveListener(Cancel);
        }

        public bool PreferCancel
        {
            get => _preferCancel;
            set
            {
                _preferCancel = value;
                if (IsShowing)
                    SelectDefaultButton();
            }
        }

        private void SelectDefaultButton()
        {
            if (EventSystem.current == null)
                return;
            var button = _preferCancel && _cancelButton.gameObject.activeSelf ? _cancelButton :
                _confirmButton.gameObject.activeSelf ? _confirmButton :
                _cancelButton.gameObject.activeSelf ? _cancelButton :
                _closeButton.gameObject.activeSelf ? _closeButton : null;
            EventSystem.current.SetSelectedGameObject(button == null ? null : button.gameObject);
        }

        private void Confirm() => Complete(OnConfirm);

        private void Cancel() => Complete(OnCancel);

        private void Complete(System.Action callback)
        {
            if (_closed)
                return;
            Hide();
            callback?.Invoke();
        }

        public override void Hide()
        {
            if (_closed)
                return;
            _closed = true;
            var eventSystem = EventSystem.current;
            var selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
            bool restoreSelection = selected == null || selected.transform.IsChildOf(transform);
            base.Hide();
            if (eventSystem != null && restoreSelection)
                eventSystem.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy
                    ? _previousSelection : null);
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
            return UIManager.Instance.PushPanel<MsgBox>(identifier, UIManager.Layer.System);
        }

        /// <summary>
        /// 打开一个MsgBox，使用给定的identifier，通过回调对其进行设置。
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="setting"></param>
        public static void Create(string identifier, System.Action<MsgBox> setting = null)
        {
            var msgbox = Create(identifier);
            setting?.Invoke(msgbox);
        }

        /// <summary>
        /// 打开一个MsgBox，使用随机生成的Identifier
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public static MsgBox Create()
        {
            return UIManager.Instance.PushPanel<MsgBox>(System.Guid.NewGuid().ToString(), UIManager.Layer.System);
        }

        /// <summary>
        /// 打开一个MsgBox，使用随机生成的Identifier，通过回调对其进行设置。
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="setting"></param>
        public static void Create(System.Action<MsgBox> setting = null)
        {
            var msgbox = Create();
            setting?.Invoke(msgbox);
        }
    }

}
