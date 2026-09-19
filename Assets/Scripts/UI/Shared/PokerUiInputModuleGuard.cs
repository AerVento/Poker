using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace Game.UI
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public sealed class PokerUiInputModuleGuard : MonoBehaviour
    {
        [SerializeField] private InputSystemUIInputModule _inputModule;

        private void Awake()
        {
            if (_inputModule == null)
                _inputModule = GetComponent<InputSystemUIInputModule>();

            // Confirm is owned by PanelShortcutHandler. Keeping a second Submit action here
            // can click the page exposed after a modal closes during the same key press.
            _inputModule.submit = null;
            _inputModule.cancel = null;
        }
    }
}
