using UnityEditor;
using UnityEditor.SceneManagement;

namespace Game.UI.Gifts.Editor
{
    public static class GiftPreviewSetup
    {
        public const string ScenePath = "Assets/Scenes/GiftPreview.unity";

        [MenuItem("Poker/Gifts/Open Gift Preview")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Poker/Gifts/Play Gift Preview")]
        public static void Play()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
