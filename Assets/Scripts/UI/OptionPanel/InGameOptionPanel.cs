using Framework.UI;
using Game.Network;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class InGameOptionPanel : SettingsPanel<InGameOptionPanel>
    {
        [SerializeField] private Button _tasks;

        protected override void OnEnable()
        {
            base.OnEnable();
            _tasks.onClick.AddListener(ShowTasks);
        }

        protected override void OnDisable()
        {
            _tasks.onClick.RemoveListener(ShowTasks);
            base.OnDisable();
        }

        private void ShowTasks()
        {
            if ((NetworkManager.singleton as NetworkRoom)?.CanOpenDailyTasks == true)
                UIManager.Instance.PushPanel<DailyTaskPanel>(UIManager.Layer.System);
        }
    }
}
