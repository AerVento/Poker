using System;
using Cysharp.Threading.Tasks;
using Game.Content;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.UI
{
    public sealed class ResourceDownloadView : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private GameObject _mask;
        [SerializeField] private Button _download;
        [SerializeField] private GameObject _progress;
        [SerializeField] private Image _fill;
        private string _address;
        private bool _interactive;

        private void OnEnable()
        {
            _download.onClick.AddListener(Download);
            if (!string.IsNullOrEmpty(_address)) Check().Forget();
            Refresh();
        }

        private void OnDisable() => _download.onClick.RemoveListener(Download);
        private void Update() => Refresh();

        // Consume clicks on the gift indicator instead of bubbling into its parent throw button.
        public void OnPointerClick(PointerEventData eventData) { }
        public void OnPointerDown(PointerEventData eventData) { }
        public void OnPointerUp(PointerEventData eventData) { }

        public void Bind(string address, bool interactive)
        {
            var changed = _address != address;
            _address = address;
            _interactive = interactive;
            if (changed && isActiveAndEnabled) Check().Forget();
            Refresh();
        }

        private async UniTaskVoid Check()
        {
            try { await RemoteAssets.CheckDownloadAsync(_address, this.GetCancellationTokenOnDestroy()); }
            catch (OperationCanceledException) { }
        }

        private void Download()
        {
            if (_interactive && !RemoteAssets.IsDownloaded(_address))
                RemoteAssets.DownloadAsync(_address).Forget();
        }

        private void Refresh()
        {
            var state = RemoteAssets.DownloadState(_address);
            var downloading = state == RemoteDownloadState.Downloading;
            _mask.SetActive(state != RemoteDownloadState.Downloaded);
            _download.gameObject.SetActive(!downloading);
            // In the gift menu this is a visual indicator, with no input or disabled tint.
            _download.enabled = _interactive;
            _download.targetGraphic.raycastTarget = _interactive;
            _progress.SetActive(downloading);
            _fill.fillAmount = RemoteAssets.DownloadProgress(_address);
        }
    }
}
