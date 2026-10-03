using Game.Content;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Chooses where Editor Play Mode reads remote content from; takes effect on the next Play Mode.</summary>
    public static class RemoteContentSourceMenu
    {
        private const string LocalPath = "Poker/Remote Artwork/Editor Content Source/Local Assets";
        private const string RemotePath = "Poker/Remote Artwork/Editor Content Source/Remote Server";

        [MenuItem(LocalPath, priority = 0)]
        private static void UseLocal() => Select(EditorContentSource.LocalAssets);

        [MenuItem(RemotePath, priority = 1)]
        private static void UseRemote() => Select(EditorContentSource.RemoteServer);

        [MenuItem(LocalPath, true)]
        private static bool ValidateLocal() => Refresh();

        [MenuItem(RemotePath, true)]
        private static bool ValidateRemote() => Refresh();

        private static void Select(EditorContentSource source)
        {
            RemoteAssets.EditorSource = source;
            Refresh();
            Debug.Log("Editor remote content source: " + (source == EditorContentSource.LocalAssets
                ? "local assets in Assets/RemoteContent." : "the server in client-network.json resource-base-url (WebGL content).") +
                (EditorApplication.isPlaying ? " Restart Play Mode to apply." : string.Empty));
        }

        // Menu check marks are drawn from the validators, which run every time the menu opens.
        private static bool Refresh()
        {
            var source = RemoteAssets.EditorSource;
            Menu.SetChecked(LocalPath, source == EditorContentSource.LocalAssets);
            Menu.SetChecked(RemotePath, source == EditorContentSource.RemoteServer);
            return !EditorApplication.isPlaying;
        }
    }
}
