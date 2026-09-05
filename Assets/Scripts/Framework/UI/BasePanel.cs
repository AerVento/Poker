using UnityEngine;

namespace Framework.UI
{
    public class BasePanel : MonoBehaviour, IPanel
    {
        public string Identifier { get; set; }

        public bool IsShowing { get; protected set; }

        public virtual void Show()
        {
            IsShowing = true;
            gameObject.SetActive(true);
            if (UIManager.Available)
                UIManager.Instance.NotifyPanelShown(this);
        }

        public virtual void Hide()
        {
            IsShowing = false;
            gameObject.SetActive(false);
            if (UIManager.Available)
                UIManager.Instance.NotifyPanelHidden(this);
        }

        internal virtual void ReleaseManagedInstance()
        {
        }

        protected virtual void OnDestroy()
        {
            if (UIManager.Available)
                UIManager.Instance.NotifyPanelDestroyed(this);
        }

        protected void DestroyMe()
        {
            if (UIManager.Available)
                UIManager.Instance.DestroyPanel(Identifier);
            else
                Destroy(gameObject);
        }
    }
}