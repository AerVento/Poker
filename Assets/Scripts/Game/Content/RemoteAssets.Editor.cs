#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Content
{
    /// <summary>Where Editor Play Mode reads remote content from. Player builds always download it.</summary>
    public enum EditorContentSource { LocalAssets, RemoteServer }

    public static partial class RemoteAssets
    {
        private const string EditorSourceKey = "Poker.RemoteContent.EditorSource";

        /// <summary>
        /// LocalAssets (default) loads the source assets in Assets/RemoteContent directly, so everything is available at
        /// once and edits show on the next play. RemoteServer uses the same download pipeline as the WebGL page.
        /// </summary>
        public static EditorContentSource EditorSource
        {
            get => (EditorContentSource)EditorPrefs.GetInt(EditorSourceKey, (int)EditorContentSource.LocalAssets);
            set => EditorPrefs.SetInt(EditorSourceKey, (int)value);
        }

        private static bool ReadsLocalAssets => EditorSource == EditorContentSource.LocalAssets;

        private static bool LocalAssetExists(string address) =>
            !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(SourcePath(address)));

        private static Object LoadLocalAsset(Entry entry)
        {
            var asset = AssetDatabase.LoadAssetAtPath(SourcePath(entry.Address), entry.Type);
            entry.LocalAsset = asset;
            return asset;
        }
    }
}
#endif
