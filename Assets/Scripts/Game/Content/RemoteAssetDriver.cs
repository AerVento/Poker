using UnityEngine;

namespace Game.Content
{
    internal sealed class RemoteAssetDriver : MonoBehaviour
    {
        private float _nextTrim;
        private void Update()
        {
            if (Time.unscaledTime < _nextTrim) return;
            _nextTrim = Time.unscaledTime + 2;
            RemoteAssets.Trim();
        }
    }
}
