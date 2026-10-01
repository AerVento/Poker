using Game.Content;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class ArtworkLoadingView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _message;
        [SerializeField] private string _loadingText = "加载中…";

        public void Present(RemoteAssetState state)
        {
            gameObject.SetActive(state != RemoteAssetState.Ready);
            if (gameObject.activeSelf) transform.SetAsLastSibling();
            if (_message != null) _message.text = state == RemoteAssetState.Failed ? "预览暂不可用" : _loadingText;
        }
    }
}
