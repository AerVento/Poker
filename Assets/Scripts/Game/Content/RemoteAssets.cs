using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using Game.Network;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Game.Content
{
    /// <summary>
    /// Client-only remote content manager: content version and catalog, per-address browser records,
    /// missing/stale/current states, shared downloads, version replacement and reference-counted residency.
    /// Consumers obtain remote assets only through this class.
    /// </summary>
    public static partial class RemoteAssets
    {
        public const string BuildOrigin = "https://poker-content.invalid";
        private const double InitializationRetrySeconds = 30;
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
        private static UniTaskCompletionSource<bool> _initialization;
        private static RemoteAssetDriver _driver;
        private static string _baseUrl;
        private static double _retryInitializationAt;
        private static bool _addressablesReady;
        private static IRemoteBundleStore _store;
        private static RemoteContentRecordBook _records;
        private static RemoteContentCatalog _catalog;
        public static string BaseUrl => _baseUrl;
        public static int ResidentCount => Entries.Count;
        /// <summary>The accepted content version, or null before a remote catalog has been loaded.</summary>
        public static string ContentVersion => _catalog?.Version.Version;

        /// <summary>A newer version of the address is ready. Holders should release their lease and acquire again.</summary>
        public static event Action<string> Updated;

        // Addresses keep product IDs ("cardfaces/jewel-court"); the source asset files use PascalCase names.
        public static string SourcePath(string address)
        {
            var slash = address.LastIndexOf('/');
            var name = string.Concat(address.Substring(slash + 1).Split('-', '_')
                .Where(part => part.Length > 0).Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
            return "Assets/RemoteContent/" + address.Substring(0, slash + 1) + name + ".asset";
        }

        public static bool IsReady(string address) => string.IsNullOrEmpty(address) ||
            Entries.TryGetValue(address, out var entry) && !entry.Retired && entry.Finished && entry.Handle.IsValid() &&
            entry.Handle.Status == AsyncOperationStatus.Succeeded;

        internal sealed class Entry
        {
            public string Address;
            public Type Type;
            public int Users;
            public double ReleasedAt;
            public RemoteContentRecord Record;
            public AsyncOperationHandle Handle;
            public readonly UniTaskCompletionSource<UnityEngine.Object> Ready = new UniTaskCompletionSource<UnityEngine.Object>();
            public bool Finished;
            // A newer version is ready: no new users, unloaded as soon as the last lease is released.
            public bool Retired;
            public bool Removed;
        }

        public sealed class Lease<T> : IDisposable where T : UnityEngine.Object
        {
            private Entry _entry;
            public T Asset { get; }
            internal Lease(Entry entry, T asset) { _entry = entry; Asset = asset; }
            public void Dispose()
            {
                if (_entry == null) return;
                Release(_entry);
                _entry = null;
            }
        }

        /// <summary>
        /// Current: the current version. Stale: the downloaded old version while the new one downloads in the background.
        /// Missing: downloads first; callers that must not wait show their default appearance meanwhile.
        /// Returns null when the content is unavailable or failed.
        /// </summary>
        public static UniTask<Lease<T>> AcquireAsync<T>(string address, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(address)) return UniTask.FromResult<Lease<T>>(null);
#if UNITY_SERVER || (UNITY_STANDALONE_LINUX && !UNITY_EDITOR)
            return UniTask.FromResult<Lease<T>>(null);
#else
            if (!Application.isPlaying) throw new InvalidOperationException("Remote assets require Play Mode.");
            EnsureDriver();
            return AcquireCore<T>(address, cancellationToken);
#endif
        }

        private static async UniTask<Lease<T>> AcquireCore<T>(string address, CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            while (true)
            {
                // During a replacement new users wait for the new version instead of keeping the old one alive.
                if (Swaps.TryGetValue(address, out var swap))
                {
                    await swap.Task.AttachExternalCancellation(cancellationToken);
                    continue;
                }
                if (!Entries.TryGetValue(address, out var entry))
                {
                    entry = new Entry { Address = address, Type = typeof(T), Users = 1 };
                    Entries.Add(address, entry);
                    Load<T>(entry).Forget();
                }
                else if (entry.Retired)
                {
                    await UniTask.WaitUntil(() => entry.Removed || !entry.Retired, cancellationToken: cancellationToken);
                    continue;
                }
                else
                {
                    if (entry.Type != typeof(T)) throw new InvalidOperationException("Resource address used with different types: " + address);
                    entry.Users++;
                }
                try
                {
                    var asset = await entry.Ready.Task.AttachExternalCancellation(cancellationToken) as T;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (asset != null) return new Lease<T>(entry, asset);
                    Release(entry);
                    return null;
                }
                catch { Release(entry); throw; }
            }
        }

        private static void EnsureDriver()
        {
            if (_driver != null) return;
            var root = new GameObject("RemoteAssets");
            UnityEngine.Object.DontDestroyOnLoad(root);
            _driver = root.AddComponent<RemoteAssetDriver>();
        }

        private static async UniTaskVoid Load<T>(Entry entry) where T : UnityEngine.Object
        {
            UnityEngine.Object asset = null;
            try
            {
                if (await Initialize())
                {
                    var latest = _catalog.Find(entry.Address);
                    var local = _records.Get(entry.Address);
                    var state = RemoteContentRules.Judge(local, latest, ClientBuild.ContentFormat);
                    if (state == RemoteContentState.Stale) QueueUpdate(entry.Address);
                    if (state == RemoteContentState.Current || state == RemoteContentState.Stale)
                    {
                        asset = await LoadRecordAsync<T>(entry, local);
                        // The old version is gone from the browser cache and the site: forget it and fetch the current one.
                        if (asset == null && state == RemoteContentState.Stale)
                        {
                            Forget(entry.Address);
                            state = RemoteContentState.Missing;
                        }
                    }
                    if (state == RemoteContentState.Missing && await FetchAsync(entry.Address, latest))
                        asset = await LoadRecordAsync<T>(entry, _records.Get(entry.Address));
                }
                if (asset == null) Debug.LogWarning("Remote content unavailable; using default: " + entry.Address);
            }
            catch (Exception exception) { Debug.LogWarning("Remote content unavailable: " + entry.Address + " / " + exception.Message); }
            finally
            {
                entry.Finished = true;
                entry.Ready.TrySetResult(asset);
                if (asset == null && entry.Users == 0) Remove(entry);
            }
        }

        private static async UniTask<UnityEngine.Object> LoadRecordAsync<T>(Entry entry, RemoteContentRecord record)
            where T : UnityEngine.Object
        {
            if (record == null) return null;
            // A bundle with the same internal name as one still unloading cannot be loaded yet.
            await WaitForUnloads();
            entry.Record = record;
            var handle = Addressables.LoadAssetAsync<T>(RemoteContentCatalog.ToLocation(record, typeof(T)));
            entry.Handle = handle;
            // AsyncOperationHandle.Task is unavailable on WebGL; poll on Unity's player loop.
            while (!handle.IsDone) await UniTask.Yield();
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null) return handle.Result;
            Addressables.Release(handle);
            entry.Handle = default;
            entry.Record = null;
            return null;
        }

        private static UniTask<bool> Initialize()
        {
            if (_initialization == null)
            {
                if (Time.realtimeSinceStartupAsDouble < _retryInitializationAt) return UniTask.FromResult(false);
                var completion = new UniTaskCompletionSource<bool>();
                _initialization = completion;
                InitializeCore(completion).Forget();
                return completion.Task;
            }
            return _initialization.Task;
        }

        private static async UniTaskVoid InitializeCore(UniTaskCompletionSource<bool> completion)
        {
            var success = false;
            try
            {
                if (!_addressablesReady)
                {
                    var configuration = NetworkConfigurationLoader.CurrentClient ??
                        await NetworkConfigurationLoader.LoadClientAsync(CancellationToken.None);
                    _baseUrl = ResolveBaseUrl(configuration.ResourceBaseUrl, Application.absoluteURL);
                    Addressables.InternalIdTransformFunc = location => TransformUrl(location.InternalId, _baseUrl);
                    var handle = Addressables.InitializeAsync(false);
                    try
                    {
                        while (!handle.IsDone) await UniTask.Yield();
                        if (handle.Status != AsyncOperationStatus.Succeeded) throw new InvalidOperationException("Addressables initialization failed.");
                    }
                    finally { if (handle.IsValid()) Addressables.Release(handle); }
                    // Only records from the remote catalog are loaded; the catalog built into the page must not answer.
                    Addressables.ClearResourceLocators();
                    EnsureProviders();
                    _store = RemoteBundleStore.Create();
                    _records = RemoteContentRecordBook.Parse(_store.ReadRecords());
                    _addressablesReady = true;
                }
                var format = ClientBuild.ContentFormat;
                if (string.IsNullOrEmpty(format)) throw new InvalidOperationException("This page has no content format stamp.");
                var version = await FetchVersionAsync();
                if (!RemoteContentRules.Accepts(version, format))
                    throw new InvalidOperationException("Remote content format " + version?.Format + " does not match page format " + format + ".");
                _catalog = await FetchCatalogAsync(version);
                Purge();
                success = true;
            }
            catch (Exception exception) { Debug.LogWarning("Remote content initialization failed: " + exception.Message); }
            if (!success)
            {
                _retryInitializationAt = Time.realtimeSinceStartupAsDouble + InitializationRetrySeconds;
                _initialization = null;
            }
            completion.TrySetResult(success);
        }

        // Fast Play Mode and the built-in catalog may not register the providers named by remote records.
        private static void EnsureProviders()
        {
            var providers = Addressables.ResourceManager.ResourceProviders;
            if (!providers.Any(p => p.ProviderId == typeof(AssetBundleProvider).FullName)) providers.Add(new AssetBundleProvider());
            if (!providers.Any(p => p.ProviderId == typeof(BundledAssetProvider).FullName)) providers.Add(new BundledAssetProvider());
        }

        private static string ContentFolderUrl
        {
            get
            {
#if UNITY_EDITOR
                // Editor Play Mode loads the Windows content build regardless of the active build target.
                const string platform = "StandaloneWindows64";
#else
                var platform = PlatformMappingService.GetPlatformPathSubFolder();
#endif
                return _baseUrl + "/" + platform;
            }
        }

        private static async UniTask<RemoteContentVersion> FetchVersionAsync()
        {
            var text = await FetchTextAsync(ContentFolderUrl + "/" + RemoteContentVersion.FileName, Guid.NewGuid().ToString("N"));
            if (!RemoteContentVersion.TryParse(text, out var version)) throw new FormatException("Invalid " + RemoteContentVersion.FileName + ".");
            return version;
        }

        private static async UniTask<RemoteContentCatalog> FetchCatalogAsync(RemoteContentVersion version) =>
            new RemoteContentCatalog(version, await FetchTextAsync(ContentFolderUrl + "/" + version.Catalog, version.Version));

        // Small text files always bypass caches; the query pins the catalog to the version that named it.
        private static async UniTask<string> FetchTextAsync(string url, string query)
        {
            for (var attempt = 0; ; attempt++)
            {
                using var request = UnityWebRequest.Get(url + "?v=" + UnityWebRequest.EscapeURL(query));
                request.timeout = 15;
                request.SetRequestHeader("Cache-Control", "no-cache");
                try { await request.SendWebRequest(); }
                catch (UnityWebRequestException) { }
                if (request.result == UnityWebRequest.Result.Success) return request.downloadHandler.text;
                if (attempt >= 2) throw new InvalidOperationException("Cannot download " + url + ": " + request.error);
                await UniTask.Delay(TimeSpan.FromSeconds(1 << attempt), ignoreTimeScale: true);
            }
        }

        public static string ResolveBaseUrl(string configured, string pageUrl)
        {
            if (string.IsNullOrWhiteSpace(configured))
                configured = string.IsNullOrEmpty(pageUrl) ? "http://localhost:18080/Content/v1" : "Content/v1";
            if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri))
            {
                if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var page))
                    throw new ArgumentException("A relative content URL requires a WebGL page URL.");
                uri = new Uri(page, configured);
            }
            if (uri.Scheme != "http" && uri.Scheme != "https") throw new ArgumentException("Content URL must use HTTP or HTTPS.");
            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("Content base URL must not contain a query or fragment.");
            return uri.AbsoluteUri.TrimEnd('/');
        }

        public static string TransformUrl(string internalId, string baseUrl)
            => internalId.StartsWith(BuildOrigin + "/", StringComparison.Ordinal)
                ? baseUrl.TrimEnd('/') + internalId.Substring(BuildOrigin.Length) : internalId;

        private static void Release(Entry entry)
        {
            entry.Users--;
            entry.ReleasedAt = Time.realtimeSinceStartupAsDouble;
            if (entry.Users > 0 || !entry.Finished) return;
            // Replaced versions are unloaded at once instead of idling in memory.
            if (entry.Retired || !entry.Handle.IsValid() || entry.Handle.Status != AsyncOperationStatus.Succeeded) Remove(entry);
        }

        private static void Remove(Entry entry)
        {
            if (Entries.TryGetValue(entry.Address, out var current) && current == entry) Entries.Remove(entry.Address);
            if (entry.Handle.IsValid()) Addressables.Release(entry.Handle);
            entry.Handle = default;
            entry.Removed = true;
        }

        internal static void Trim()
        {
            var idle = Entries.Values.Where(e => e.Users == 0 && e.Finished).OrderByDescending(e => e.ReleasedAt).ToArray();
            for (var i = 0; i < idle.Length; i++)
                if (idle[i].Retired || i >= 2 || Time.realtimeSinceStartupAsDouble - idle[i].ReleasedAt > 15) Remove(idle[i]);
        }

        // AssetBundleProvider unloads asynchronously and only tracks the URL it unloaded.
        private sealed class BundleUnloads : AssetBundleProvider
        {
            public static bool Pending => UnloadingBundles.Count > 0;
        }

        private static async UniTask WaitForUnloads()
        {
            while (BundleUnloads.Pending) await UniTask.Yield();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Entries.Clear();
            Transfers.Clear();
            Swaps.Clear();
            UpdateQueue.Clear();
            Queued.Clear();
            FailedUpdates.Clear();
            FailedDownloads.Clear();
            _updating = false;
            _initialization = null;
            _driver = null;
            _baseUrl = null;
            _retryInitializationAt = 0;
            _addressablesReady = false;
            _store = null;
            _records = null;
            _catalog = null;
            Updated = null;
        }
    }
}
