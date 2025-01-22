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
        }

        public virtual void Hide()
        {
            IsShowing = false;
            gameObject.SetActive(false);
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