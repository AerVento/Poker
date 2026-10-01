using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Game
{
    /// <summary>
    /// Compares this WebGL page with page-version.txt on the site. Pages and the server are deployed together,
    /// so an outdated page must be refreshed before it logs in.
    /// </summary>
    public static class PageVersion
    {
        public const string FileName = "page-version.txt";
        // Added to the URL by a refresh, so a page that is still old afterwards is not asked to refresh forever.
        private const string ReloadParameter = "pokerPage";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void PokerPage_Reload(string parameter, string version);
#endif

        /// <summary>
        /// The deployed page version when it differs from this page, otherwise null. Unreadable files never block the
        /// game, and neither does a mismatch that a refresh for that same version did not resolve.
        /// </summary>
        public static async UniTask<string> FindNewerAsync(CancellationToken cancellationToken = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var current = ClientBuild.PageVersion;
            if (string.IsNullOrEmpty(current) || !Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var page))
                return null;
            var url = new UriBuilder(new Uri(page, FileName)) { Query = "v=" + Guid.NewGuid().ToString("N") }.Uri;
            using var request = UnityWebRequest.Get(url);
            request.timeout = 10;
            request.SetRequestHeader("Cache-Control", "no-cache");
            try { await request.SendWebRequest().WithCancellation(cancellationToken); }
            catch (UnityWebRequestException) { return null; }
            var deployed = request.downloadHandler.text.Trim();
            if (deployed.Length == 0 || deployed == current) return null;
            if (ReloadedFor(page, deployed))
            {
                Debug.LogWarning("The site announces page " + deployed + " but a refresh still loaded page " + current +
                    "; check that the new page was uploaded and is not cached.");
                return null;
            }
            return deployed;
#else
            await UniTask.CompletedTask;
            return null;
#endif
        }

        private static bool ReloadedFor(Uri page, string deployed)
        {
            foreach (var pair in page.Query.TrimStart('?').Split('&'))
            {
                var separator = pair.IndexOf('=');
                if (separator > 0 && pair.Substring(0, separator) == ReloadParameter &&
                    Uri.UnescapeDataString(pair.Substring(separator + 1)) == deployed)
                    return true;
            }
            return false;
        }

        public static void Reload(string deployed)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PokerPage_Reload(ReloadParameter, deployed ?? string.Empty);
#endif
        }
    }
}
