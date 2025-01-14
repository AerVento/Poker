using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Framework.UI;
using UnityEngine.UI;
namespace Game.UI
{
    public class RoomPanel : SingletonPanel<RoomPanel>
    {
        [SerializeField]
        private Button _back;



        private void OnEnable()
        {
            _back.onClick.AddListener(Back);
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
        }

        private void Back()
        {
            UIManager.Instance.HidePanel<RoomPanel>();
            UIManager.Instance.ShowPanel<TitlePanel>();
        }
    }
}