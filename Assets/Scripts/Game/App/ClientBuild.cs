using System;
using Game.Content;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Stamps written into a player build by ClientBuildStamp: the WebGL page version and the content format this page reads.
    /// </summary>
    public static class ClientBuild
    {
        public const string ResourceName = "ClientBuild";
        private static bool _loaded;
        private static string _pageVersion;
        private static string _contentFormat;
#if UNITY_EDITOR
        // Play Mode reads the format of the current Editor scripts instead of a stale stamp.
        public static Func<string> EditorContentFormat;
#endif

        public static string PageVersion
        {
            get
            {
                Load();
                return _pageVersion;
            }
        }

        public static string ContentFormat
        {
            get
            {
#if UNITY_EDITOR
                if (EditorContentFormat != null) return EditorContentFormat();
#endif
                Load();
                return _contentFormat;
            }
        }

        public static string Format(string pageVersion, string contentFormat) =>
            "page=" + pageVersion + "\nformat=" + contentFormat + "\n";

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            var stamp = Resources.Load<TextAsset>(ResourceName);
            if (stamp == null) return;
            var values = RemoteContentVersion.ParseLines(stamp.text);
            values.TryGetValue("page", out _pageVersion);
            values.TryGetValue("format", out _contentFormat);
            Resources.UnloadAsset(stamp);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _loaded = false;
            _pageVersion = null;
            _contentFormat = null;
        }
    }
}
