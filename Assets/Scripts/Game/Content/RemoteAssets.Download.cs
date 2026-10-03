using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Content
{
    public enum RemoteDownloadState { Unknown, Available, Downloading, Downloaded, Failed }

    public static partial class RemoteAssets
    {
        private const double DrainTimeoutSeconds = 30;

        private sealed class Transfer
        {
            public RemoteContentRecord Target;
            public float Progress;
            public UniTaskCompletionSource<bool> Done;
        }

        // One download per address; it always targets the version in the accepted catalog.
        private static readonly Dictionary<string, Transfer> Transfers = new Dictionary<string, Transfer>();
        // Addresses whose old version is being unloaded and replaced; acquirers wait for the new one.
        private static readonly Dictionary<string, UniTaskCompletionSource<bool>> Swaps = new Dictionary<string, UniTaskCompletionSource<bool>>();
        private static readonly Queue<string> UpdateQueue = new Queue<string>();
        private static readonly HashSet<string> Queued = new HashSet<string>();
        // A failed background update is retried at the next lobby check; a failed first download on demand.
        private static readonly HashSet<string> FailedUpdates = new HashSet<string>();
        private static readonly HashSet<string> FailedDownloads = new HashSet<string>();
        private static bool _updating;

        private static RemoteContentState StateOf(string address) => _catalog == null || _records == null
            ? RemoteContentState.Unavailable
            : RemoteContentRules.Judge(_records.Get(address), _catalog.Find(address), ClientBuild.ContentFormat);

        public static RemoteDownloadState DownloadState(string address)
        {
            if (string.IsNullOrEmpty(address)) return RemoteDownloadState.Downloaded;
#if UNITY_EDITOR
            if (ReadsLocalAssets) return LocalAssetExists(address) ? RemoteDownloadState.Downloaded : RemoteDownloadState.Failed;
#endif
            var state = StateOf(address);
            if (state == RemoteContentState.Current || state == RemoteContentState.Stale) return RemoteDownloadState.Downloaded;
            if (Transfers.ContainsKey(address)) return RemoteDownloadState.Downloading;
            if (state == RemoteContentState.Missing)
                return FailedDownloads.Contains(address) ? RemoteDownloadState.Failed : RemoteDownloadState.Available;
            return _initialization == null && _retryInitializationAt > 0 ? RemoteDownloadState.Failed : RemoteDownloadState.Unknown;
        }

        /// <summary>A usable version is in the browser: the current one, or an old one that is being updated.</summary>
        public static bool IsDownloaded(string address) => DownloadState(address) == RemoteDownloadState.Downloaded;

        public static float DownloadProgress(string address) => string.IsNullOrEmpty(address) ? 1f :
            Transfers.TryGetValue(address, out var transfer) ? transfer.Progress : IsDownloaded(address) ? 1f : 0f;

        // Reads only the local records against the remote catalog; never downloads artwork merely to browse an item.
        public static UniTask<bool> CheckDownloadAsync(string address, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(address)) return UniTask.FromResult(true);
#if UNITY_SERVER || (UNITY_STANDALONE_LINUX && !UNITY_EDITOR)
            return UniTask.FromResult(false);
#else
            return Application.isPlaying ? CheckDownloadCore(address, cancellationToken) : UniTask.FromResult(false);
#endif
        }

        private static async UniTask<bool> CheckDownloadCore(string address, CancellationToken cancellationToken)
        {
#if UNITY_EDITOR
            if (ReadsLocalAssets) return IsDownloaded(address);
#endif
            await Initialize().AttachExternalCancellation(cancellationToken);
            return IsDownloaded(address);
        }

        /// <summary>Downloads a missing address without loading it. Cancellation detaches only this caller.</summary>
        public static UniTask<bool> DownloadAsync(string address, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(address)) return UniTask.FromResult(true);
#if UNITY_SERVER || (UNITY_STANDALONE_LINUX && !UNITY_EDITOR)
            return UniTask.FromResult(false);
#else
            return Application.isPlaying ? DownloadCore(address, cancellationToken) : UniTask.FromResult(false);
#endif
        }

        private static async UniTask<bool> DownloadCore(string address, CancellationToken cancellationToken)
        {
#if UNITY_EDITOR
            if (ReadsLocalAssets) return IsDownloaded(address);
#endif
            if (!await Initialize().AttachExternalCancellation(cancellationToken)) return false;
            switch (StateOf(address))
            {
                case RemoteContentState.Current: return true;
                case RemoteContentState.Stale: QueueUpdate(address); return true;
                case RemoteContentState.Missing:
                    return await FetchAsync(address, _catalog.Find(address)).AttachExternalCancellation(cancellationToken);
                default: return false;
            }
        }

        /// <summary>
        /// Lobby entry: adopt a newer content version of the same format, then update every downloaded address
        /// that changed. Addresses never downloaded stay missing.
        /// </summary>
        public static UniTask CheckForUpdatesAsync()
        {
#if UNITY_SERVER || (UNITY_STANDALONE_LINUX && !UNITY_EDITOR)
            return UniTask.CompletedTask;
#else
            return Application.isPlaying ? CheckForUpdatesCore() : UniTask.CompletedTask;
#endif
        }

        private static async UniTask CheckForUpdatesCore()
        {
#if UNITY_EDITOR
            if (ReadsLocalAssets) return;
#endif
            try
            {
                if (_catalog == null)
                {
                    await Initialize();
                    return;
                }
                if (_initialization != null && !_initialization.Task.Status.IsCompleted()) return;
                var version = await FetchVersionAsync();
                if (!RemoteContentRules.ShouldSwitch(_catalog.Version, version, ClientBuild.ContentFormat)) return;
                _catalog = await FetchCatalogAsync(version);
                FailedUpdates.Clear();
                FailedDownloads.Clear();
                Purge();
                foreach (var record in _records.All.ToArray())
                    if (StateOf(record.Address) == RemoteContentState.Stale) QueueUpdate(record.Address);
            }
            catch (Exception exception) { Debug.LogWarning("Remote content update check failed: " + exception.Message); }
        }

        private static void QueueUpdate(string address)
        {
            if (FailedUpdates.Contains(address) || !Queued.Add(address)) return;
            UpdateQueue.Enqueue(address);
            if (!_updating) RunUpdates().Forget();
        }

        // Background updates run one at a time so they do not compete with downloads the player is waiting for.
        private static async UniTaskVoid RunUpdates()
        {
            _updating = true;
            try
            {
                while (UpdateQueue.Count > 0)
                {
                    var address = UpdateQueue.Dequeue();
                    Queued.Remove(address);
                    if (StateOf(address) != RemoteContentState.Stale) continue;
                    if (!await FetchAsync(address, _catalog.Find(address))) FailedUpdates.Add(address);
                }
            }
            finally { _updating = false; }
        }

        private static UniTask<bool> FetchAsync(string address, RemoteContentRecord target)
        {
            if (target == null) return UniTask.FromResult(false);
            if (Transfers.TryGetValue(address, out var running) && running.Target.SameContent(target)) return running.Done.Task;
            var transfer = new Transfer { Target = target, Done = new UniTaskCompletionSource<bool>() };
            Transfers[address] = transfer;
            FailedDownloads.Remove(address);
            RunTransfer(address, transfer).Forget();
            return transfer.Done.Task;
        }

        private static async UniTaskVoid RunTransfer(string address, Transfer transfer)
        {
            var success = false;
            try
            {
                var target = transfer.Target;
                var local = _records.Get(address);
                var bundles = target.Bundles;
                for (var index = 0; index < bundles.Count; index++)
                {
                    var bundle = bundles[index];
                    if (local != null && local.Bundles.Any(bundle.Same)) continue;
                    var url = TransformUrl(bundle.Url, _baseUrl);
                    var offset = index;
                    if (!await _store.DownloadAsync(url, RemoteContentCatalog.Options(bundle),
                        value => transfer.Progress = Mathf.Min(0.99f, (offset + value) / bundles.Count)))
                        throw new InvalidOperationException("Download failed: " + url);
                }
                success = await Commit(address, target);
            }
            catch (Exception exception) { Debug.LogWarning("Remote download failed: " + address + " / " + exception.Message); }
            if (Transfers.TryGetValue(address, out var current) && current == transfer) Transfers.Remove(address);
            if (!success && !RemoteContentRules.IsUsable(_records.Get(address), ClientBuild.ContentFormat)) FailedDownloads.Add(address);
            transfer.Progress = success ? 1f : 0f;
            transfer.Done.TrySetResult(success);
        }

        /// <summary>
        /// The new version is downloaded but not loaded. Unload the old version once its users release it,
        /// delete it from the browser cache, then point the record at the new version. The next acquire loads it.
        /// </summary>
        private static async UniTask<bool> Commit(string address, RemoteContentRecord target)
        {
            var old = _records.Get(address);
            if (old != null && old.SameContent(target)) return true;
            // A download that finished after the catalog moved on must not replace a newer record.
            if (_catalog?.Find(address)?.SameContent(target) != true) return false;
            var swap = new UniTaskCompletionSource<bool>();
            Swaps[address] = swap;
            try
            {
                if (Entries.TryGetValue(address, out var entry))
                {
                    // An entry still loading the old version finishes first; one that failed holds nothing.
                    while (entry.Record != null && !entry.Finished) await UniTask.Yield();
                    if (entry.Record != null && !entry.Removed)
                    {
                        entry.Retired = true;
                        Updated?.Invoke(address);
                        if (entry.Users == 0) Remove(entry);
                        var deadline = Time.realtimeSinceStartupAsDouble + DrainTimeoutSeconds;
                        while (!entry.Removed && Time.realtimeSinceStartupAsDouble < deadline) await UniTask.Yield();
                        if (!entry.Removed)
                        {
                            // A holder did not release the old version; keep it and retry at the next lobby check.
                            entry.Retired = false;
                            throw new TimeoutException("Old version is still in use: " + address);
                        }
                    }
                }
                await WaitForUnloads();
                if (old != null) DeleteBundles(old.BundlesNotIn(target));
                _records.Set(target);
                SaveRecords();
                return true;
            }
            finally
            {
                Swaps.Remove(address);
                swap.TrySetResult(true);
            }
        }

        // Drops a record whose version can no longer be used and deletes its bundles from the browser cache.
        private static void Forget(string address)
        {
            var record = _records.Get(address);
            if (record == null) return;
            _records.Remove(address);
            SaveRecords();
            DeleteBundles(record.Bundles);
        }

        // Records of another content format, or addresses the accepted catalog no longer has, cannot be loaded by this page.
        private static void Purge()
        {
            var format = ClientBuild.ContentFormat;
            foreach (var record in _records.All.ToArray())
                if (!Entries.ContainsKey(record.Address) &&
                    (!RemoteContentRules.IsUsable(record, format) || _catalog.Find(record.Address) == null))
                    Forget(record.Address);
        }

        private static void DeleteBundles(IEnumerable<RemoteBundleRecord> bundles)
        {
            foreach (var bundle in bundles)
            {
                // Another address may still use a bundle shared with this one.
                if (_records.All.Any(record => record.Bundles.Any(bundle.Same))) continue;
                try { _store.Delete(TransformUrl(bundle.Url, _baseUrl), RemoteContentCatalog.Options(bundle)); }
                catch (Exception exception) { Debug.LogWarning("Cannot delete cached bundle: " + bundle.Url + " / " + exception.Message); }
            }
        }

        private static void SaveRecords()
        {
            try { _store.WriteRecords(_records.Serialize()); }
            catch (Exception exception) { Debug.LogWarning("Cannot save remote content records: " + exception.Message); }
        }
    }
}
