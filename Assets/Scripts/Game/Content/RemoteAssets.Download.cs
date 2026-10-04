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

        // One download per address; it always targets the version in the accepted catalog. Downloading never commits.
        private static readonly Dictionary<string, Transfer> Transfers = new Dictionary<string, Transfer>();
        // Targets of the batches being downloaded or committed; their bundles are never deleted meanwhile.
        private static readonly List<IReadOnlyList<RemoteContentRecord>> Batches = new List<IReadOnlyList<RemoteContentRecord>>();
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

        /// <summary>
        /// Downloaded: the address and everything it depends on have compatible versions in the browser (current ones,
        /// or old ones being updated). Downloading: a package it needs is being downloaded or committed.
        /// </summary>
        public static RemoteDownloadState DownloadState(string address)
        {
            if (string.IsNullOrEmpty(address)) return RemoteDownloadState.Downloaded;
#if UNITY_EDITOR
            if (ReadsLocalAssets) return LocalAssetExists(address) ? RemoteDownloadState.Downloaded : RemoteDownloadState.Failed;
#endif
            if (StateOf(address) == RemoteContentState.Unavailable)
                return _initialization == null && _retryInitializationAt > 0 ? RemoteDownloadState.Failed : RemoteDownloadState.Unknown;
            var plan = Plan(address);
            if (!plan.Success) return RemoteDownloadState.Failed;
            if (plan.Updates.Count == 0) return RemoteDownloadState.Downloaded;
            if (plan.Updates.Any(IsFetching)) return RemoteDownloadState.Downloading;
            return FailedDownloads.Contains(address) ? RemoteDownloadState.Failed : RemoteDownloadState.Available;
        }

        /// <summary>A usable version is in the browser: the current one, or an old one that is being updated.</summary>
        public static bool IsDownloaded(string address) => DownloadState(address) == RemoteDownloadState.Downloaded;

        /// <summary>Progress over every package the address still needs.</summary>
        public static float DownloadProgress(string address)
        {
            if (string.IsNullOrEmpty(address) || IsDownloaded(address)) return 1f;
            if (_catalog == null || _records == null) return 0f;
            var plan = Plan(address);
            if (!plan.Success || plan.Updates.Count == 0) return 0f;
            // A package downloaded but waiting for its batch to commit counts as nearly done.
            return plan.Updates.Average(a => Transfers.TryGetValue(a, out var transfer) ? transfer.Progress : IsFetching(a) ? 0.99f : 0f);
        }

        private static bool IsFetching(string address) => Batches.Any(batch => batch.Any(target => target.Address == address));

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

        /// <summary>Downloads what an address still needs without loading it. Cancellation detaches only this caller.</summary>
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
            if (StateOf(address) == RemoteContentState.Unavailable) return false;
            var plan = Plan(address);
            if (!plan.Success) return false;
            QueueStale(plan);
            return plan.Updates.Count == 0 ||
                await FetchAsync(address, plan.Updates).AttachExternalCancellation(cancellationToken);
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
                if (RemoteContentRules.ShouldSwitch(_catalog.Version, version, ClientBuild.ContentFormat))
                {
                    _catalog = await FetchCatalogAsync(version);
                    FailedDownloads.Clear();
                    Purge();
                }
                // Updates that failed earlier (download error, old version still in use) are retried at every lobby entry.
                FailedUpdates.Clear();
                // Packages others depend on first: an update then rarely leaves a dependent incompatible for a while.
                foreach (var record in _records.All.OrderByDescending(r => DependentCount(r.Address)).ToArray())
                    if (StateOf(record.Address) == RemoteContentState.Stale) QueueUpdate(record.Address);
            }
            catch (Exception exception) { Debug.LogWarning("Remote content update check failed: " + exception.Message); }
        }

        private static int DependentCount(string address) =>
            _records.All.Count(record => record.Dependencies != null && record.Dependencies.Any(d => d?.Address == address));

        private static void QueueUpdate(string address)
        {
            if (FailedUpdates.Contains(address) || !Queued.Add(address)) return;
            UpdateQueue.Enqueue(address);
            if (!_updating) RunUpdates().Forget();
        }

        // Background updates run one batch at a time so they do not compete with downloads the player is waiting for.
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
                    // Moves the dependencies it needs and the local packages it would break, keeping all records compatible.
                    var plan = RemoteContentPlanner.ForUpdate(address, _records.All.Select(r => r.Address).ToArray(),
                        LocalRecord, candidate => _catalog?.Find(candidate));
                    if (!plan.Success) Debug.LogWarning("Remote content update skipped: " + plan.Error);
                    if (!plan.Success || !await FetchAsync(null, plan.Updates))
                        foreach (var failed in plan.Success ? plan.Updates : new[] { address }) FailedUpdates.Add(failed);
                }
            }
            finally { _updating = false; }
        }

        /// <summary>
        /// Downloads the latest version of every address, then commits them together. If any download or drain fails
        /// nothing is committed, and the bundles downloaded for this batch alone are deleted again.
        /// </summary>
        private static async UniTask<bool> FetchAsync(string requester, IReadOnlyCollection<string> addresses)
        {
            var targets = addresses.Select(address => _catalog?.Find(address)).ToArray();
            if (targets.Length == 0 || targets.Any(target => target == null)) return false;
            if (requester != null) FailedDownloads.Remove(requester);
            Batches.Add(targets);
            var success = false;
            try
            {
                var downloads = await UniTask.WhenAll(targets.Select(DownloadTarget));
                success = downloads.All(done => done) && await Commit(targets);
            }
            catch (Exception exception) { Debug.LogWarning("Remote download failed: " + string.Join(", ", addresses) + " / " + exception.Message); }
            finally
            {
                Batches.Remove(targets);
                if (!success)
                {
                    DeleteBundles(targets.SelectMany(target => target.Bundles));
                    if (requester != null) FailedDownloads.Add(requester);
                }
            }
            return success;
        }

        private static UniTask<bool> DownloadTarget(RemoteContentRecord target)
        {
            if (Transfers.TryGetValue(target.Address, out var running) && running.Target.SameContent(target)) return running.Done.Task;
            var transfer = new Transfer { Target = target, Done = new UniTaskCompletionSource<bool>() };
            Transfers[target.Address] = transfer;
            RunTransfer(transfer).Forget();
            return transfer.Done.Task;
        }

        private static async UniTaskVoid RunTransfer(Transfer transfer)
        {
            var target = transfer.Target;
            var success = false;
            try
            {
                var local = _records.Get(target.Address);
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
                success = true;
            }
            catch (Exception exception) { Debug.LogWarning("Remote download failed: " + target.Address + " / " + exception.Message); }
            if (Transfers.TryGetValue(target.Address, out var current) && current == transfer) Transfers.Remove(target.Address);
            transfer.Progress = success ? 1f : 0f;
            transfer.Done.TrySetResult(success);
        }

        /// <summary>
        /// The new versions are downloaded but not loaded. Retire the loaded old versions together with every loaded
        /// package that depends on them, wait until their users release them, then point all records at the new versions
        /// at once and delete the old bundles no record uses any more. The next acquire loads the new versions.
        /// </summary>
        private static async UniTask<bool> Commit(IReadOnlyList<RemoteContentRecord> targets)
        {
            List<RemoteContentRecord> pending;
            while (true)
            {
                // A download that finished after the catalog moved on must not replace a newer record.
                if (targets.Any(target => _catalog?.Find(target.Address)?.SameContent(target) != true)) return false;
                pending = targets.Where(target => _records.Get(target.Address)?.SameContent(target) != true).ToList();
                if (pending.Count == 0) return true;
                var busy = pending.Select(target => Swaps.TryGetValue(target.Address, out var other) ? other : null)
                    .FirstOrDefault(other => other != null);
                if (busy == null) break;
                await busy.Task;
            }
            var swap = new UniTaskCompletionSource<bool>();
            foreach (var target in pending) Swaps[target.Address] = swap;
            var retired = new List<Entry>();
            try
            {
                foreach (var target in pending)
                {
                    if (!Entries.TryGetValue(target.Address, out var entry)) continue;
                    // An entry still loading the old version finishes first; one that failed holds nothing.
                    while (entry.Record != null && !entry.Finished) await UniTask.Yield();
                    if (entry.Record != null && !entry.Removed) retired.Add(entry);
                }
                AddDependents(retired);
                foreach (var entry in retired) entry.Retired = true;
                foreach (var entry in retired) Updated?.Invoke(entry.Address);
                foreach (var entry in retired)
                    if (entry.Users == 0 && entry.Finished && !entry.Removed) Remove(entry);
                var deadline = Time.realtimeSinceStartupAsDouble + DrainTimeoutSeconds;
                while (retired.Any(entry => !entry.Removed) && Time.realtimeSinceStartupAsDouble < deadline) await UniTask.Yield();
                var held = retired.Where(entry => !entry.Removed).ToArray();
                if (held.Length > 0)
                {
                    // A holder did not release an old version; keep them all and retry at the next lobby check.
                    foreach (var entry in held) entry.Retired = false;
                    throw new TimeoutException("Old version is still in use: " + string.Join(", ", held.Select(e => e.Address)));
                }
                await WaitForUnloads();
                var old = pending.Select(target => _records.Get(target.Address)).Where(record => record != null).ToArray();
                foreach (var target in pending) _records.Set(target);
                SaveRecords();
                // After the records move, so a bundle the old and new versions share stays.
                DeleteBundles(old.SelectMany(record => record.Bundles));
                return true;
            }
            finally
            {
                foreach (var target in pending) Swaps.Remove(target.Address);
                swap.TrySetResult(true);
            }
        }

        /// <summary>
        /// Adds every entry that holds a retired one, directly or not: it was initialized with the old version and must
        /// load again with the new one. Entries still loading are told to drop their dependencies and start over.
        /// </summary>
        private static void AddDependents(List<Entry> retired)
        {
            for (var index = 0; index < retired.Count; index++)
            {
                var dependency = retired[index];
                foreach (var entry in Entries.Values.ToArray())
                {
                    if (entry.Removed || retired.Contains(entry) || !entry.Dependencies.Any(lease => lease.Entry == dependency)) continue;
                    if (entry.Finished && entry.Record != null || entry.Finished && entry.LocalAsset != null) retired.Add(entry);
                    else entry.Restart = true;
                }
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
            foreach (var bundle in bundles.ToArray())
            {
                // Another address may still use a bundle shared with this one, or a batch may be about to commit it.
                if (_records.All.Any(record => record.Bundles.Any(bundle.Same)) ||
                    Batches.Any(batch => batch.Any(target => target.Bundles.Any(bundle.Same))))
                    continue;
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
