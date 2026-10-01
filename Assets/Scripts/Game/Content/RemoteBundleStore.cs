using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.ResourceProviders;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Game.Content
{
    /// <summary>
    /// Platform storage for content records and downloaded bundles. Downloading never loads a bundle, so a new
    /// version can arrive while the old one, which has the same internal bundle name, is still loaded.
    /// </summary>
    internal interface IRemoteBundleStore
    {
        string ReadRecords();
        void WriteRecords(string json);
        UniTask<bool> DownloadAsync(string url, AssetBundleRequestOptions options, Action<float> progress);
        void Delete(string url, AssetBundleRequestOptions options);
    }

    internal static class RemoteBundleStore
    {
        public const string RecordsKey = "poker.content.records";

        public static IRemoteBundleStore Create()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new BrowserBundleStore();
#elif UNITY_EDITOR || !(UNITY_SERVER || UNITY_STANDALONE_LINUX)
            return new CachingBundleStore();
#else
            return null; // Servers never download content.
#endif
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    // Bundles live in Unity's browser cache (Cache Storage + the UnityCache IndexedDB), keyed by URL; records in localStorage.
    // RemoteContent.jslib reuses Unity's own cachedFetch, so a later bundle load is served from the same entry.
    internal sealed class BrowserBundleStore : IRemoteBundleStore
    {
        private const float StallSeconds = 30f;
        [DllImport("__Internal")] private static extern string PokerContent_ReadRecords(string key);
        [DllImport("__Internal")] private static extern void PokerContent_WriteRecords(string key, string json);
        [DllImport("__Internal")] private static extern int PokerContent_Download(string url);
        [DllImport("__Internal")] private static extern float PokerContent_Progress(int id);
        [DllImport("__Internal")] private static extern void PokerContent_Release(int id);
        [DllImport("__Internal")] private static extern void PokerContent_Delete(string url);

        public string ReadRecords() => PokerContent_ReadRecords(RemoteBundleStore.RecordsKey);
        public void WriteRecords(string json) => PokerContent_WriteRecords(RemoteBundleStore.RecordsKey, json);

        public async UniTask<bool> DownloadAsync(string url, AssetBundleRequestOptions options, Action<float> progress)
        {
            var id = PokerContent_Download(url);
            try
            {
                var last = 0f;
                var changedAt = Time.realtimeSinceStartup;
                while (true)
                {
                    var value = PokerContent_Progress(id);
                    if (value < 0) return false;
                    if (value > 1) return true;
                    if (value != last) { last = value; changedAt = Time.realtimeSinceStartup; }
                    else if (Time.realtimeSinceStartup - changedAt > StallSeconds) return false;
                    progress?.Invoke(value);
                    await UniTask.Yield();
                }
            }
            finally { PokerContent_Release(id); }
        }

        public void Delete(string url, AssetBundleRequestOptions options) => PokerContent_Delete(url);
    }
#elif UNITY_EDITOR || !(UNITY_SERVER || UNITY_STANDALONE_LINUX)
    // Editor and Windows development builds: Unity's AssetBundle cache and PlayerPrefs.
    internal sealed class CachingBundleStore : IRemoteBundleStore
    {
        public string ReadRecords() => PlayerPrefs.GetString(RemoteBundleStore.RecordsKey, string.Empty);

        public void WriteRecords(string json)
        {
            PlayerPrefs.SetString(RemoteBundleStore.RecordsKey, json);
            PlayerPrefs.Save();
        }

        public async UniTask<bool> DownloadAsync(string url, AssetBundleRequestOptions options, Action<float> progress)
        {
            if (options == null || string.IsNullOrEmpty(options.Hash)) return false;
            var cached = new CachedAssetBundle(options.BundleName, Hash128.Parse(options.Hash));
            if (Caching.IsVersionCached(cached)) return true;
            using var request = UnityWebRequestAssetBundle.GetAssetBundle(url, cached, options.Crc);
            ((DownloadHandlerAssetBundle)request.downloadHandler).autoLoadAssetBundle = false;
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                progress?.Invoke(request.downloadProgress);
                await UniTask.Yield();
            }
            return request.result == UnityWebRequest.Result.Success;
        }

        public void Delete(string url, AssetBundleRequestOptions options)
        {
            if (options != null && !string.IsNullOrEmpty(options.Hash))
                Caching.ClearCachedVersion(options.BundleName, Hash128.Parse(options.Hash));
        }
    }
#endif
}
