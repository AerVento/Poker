using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Content
{
    public enum RemoteAssetState { Loading, Ready, Failed }

    // A view owns one binding. Disposing it detaches only this consumer from a shared download.
    // When a newer version is ready the binding releases the old one, presents the default and loads the new one.
    public sealed class RemoteAssetBinding<T> : IDisposable where T : UnityEngine.Object
    {
        private CancellationTokenSource _cancellation;
        private RemoteAssets.Lease<T> _lease;
        private string _address;
        private Action<T> _present;
        private Action<RemoteAssetState> _stateChanged;

        public void Bind(string address, Action<T> present, Action<RemoteAssetState> stateChanged = null)
        {
            if (_cancellation != null && _address == address) return;
            Dispose();
            _address = address;
            stateChanged?.Invoke(string.IsNullOrEmpty(address) || !Application.isPlaying ? RemoteAssetState.Ready : RemoteAssetState.Loading);
            present(null);
            if (string.IsNullOrEmpty(address) || !Application.isPlaying) return;
            _present = present;
            _stateChanged = stateChanged;
            RemoteAssets.Updated += OnUpdated;
            Start();
        }

        private void Start()
        {
            _cancellation = new CancellationTokenSource();
            Load(_address, _present, _stateChanged, _cancellation.Token).Forget();
        }

        private void OnUpdated(string address)
        {
            if (address != _address || _cancellation == null) return;
            Stop();
            _present(null);
            _stateChanged?.Invoke(RemoteAssetState.Loading);
            Start();
        }

        private async UniTaskVoid Load(string address, Action<T> present, Action<RemoteAssetState> stateChanged, CancellationToken token)
        {
            try
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    stateChanged?.Invoke(RemoteAssetState.Loading);
                    var lease = await RemoteAssets.AcquireAsync<T>(address, token);
                    if (token.IsCancellationRequested) { lease?.Dispose(); return; }
                    if (lease != null)
                    {
                        _lease = lease;
                        present(lease.Asset);
                        stateChanged?.Invoke(RemoteAssetState.Ready);
                        return;
                    }
                    stateChanged?.Invoke(RemoteAssetState.Failed);
                    if (attempt < 2) await UniTask.Delay(TimeSpan.FromSeconds(30), ignoreTimeScale: true, cancellationToken: token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private void Stop()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _lease?.Dispose();
            _lease = null;
        }

        public void Dispose()
        {
            if (_present != null) RemoteAssets.Updated -= OnUpdated;
            Stop();
            _address = null;
            _present = null;
            _stateChanged = null;
        }
    }
}
