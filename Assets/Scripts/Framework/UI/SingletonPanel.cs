using System.Buffers.Text;
using Framework.Singleton;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// Class for panels only have one instance, and makes it easier for extern to access the panel.
    /// This is temporary singleton panel behaviour because panels can be created and destroyed by UIManager.
    /// </summary>
    /// <typeparam name="T">The panel type want to be singleton.</typeparam>
    public abstract class SingletonPanel<T> : BasePanel where T : MonoBehaviour
    {
        private static T _instance;
        
        /// <summary>
        /// Get panel singleton instance. Returns null when singleton doesn't exists. 
        /// </summary>
        public static T Instance => _instance;

        protected virtual void Awake()
        {
            if(_instance == null)
                _instance = this as T;
            else
            {
                DestroyMe();
                Debug.LogWarning($"Multiple singleton panel {typeof(T)} detected. Duplicate one will be destroyed.");
            }
        }

        protected virtual void OnDestroy()
        {
            _instance = null;
        }
    }
}