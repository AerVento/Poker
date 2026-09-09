using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.Gifts.Editor
{
    public static class RottenEggGiftDemoEditor
    {
        public const string ScenePath = "Assets/Scenes/RottenEggGiftDemo.unity";
        public const string PrefabPath = "Assets/Prefabs/Gifts/RottenEggGiftEffect.prefab";

        [MenuItem("Poker/Gifts/Play Rotten Egg Demo")]
        public static void OpenAndPlay()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateAssets();
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }

        // Called once to serialize a portable prefab and a normal Unity scene.
        public static void CreateAssets()
        {
            var egg = ImportSprite("Assets/Art/Gifts/RottenEgg.png");
            var splat = ImportSprite("Assets/Art/Gifts/RottenEggSplat.png");
            var source = new GameObject("RottenEggGiftEffect", typeof(RectTransform), typeof(RottenEggGiftEffect));
            var effect = source.GetComponent<RottenEggGiftEffect>();
            var settings = new SerializedObject(effect);
            settings.FindProperty("_eggSprite").objectReferenceValue = egg;
            settings.FindProperty("_splatSprite").objectReferenceValue = splat;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
            Object.DestroyImmediate(source);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            cameraObject.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            cameraObject.GetComponent<Camera>().backgroundColor = new Color32(12, 24, 29, 255);
            var lightObject = new GameObject("Directional Light", typeof(Light));
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.GetComponent<Light>().type = LightType.Directional;
            var demoObject = new GameObject("Rotten Egg Gift Demo", typeof(RottenEggGiftDemo));
            SceneManager.MoveGameObjectToScene(demoObject, scene);
            var demoSettings = new SerializedObject(demoObject.GetComponent<RottenEggGiftDemo>());
            demoSettings.FindProperty("_effectPrefab").objectReferenceValue = prefab.GetComponent<RottenEggGiftEffect>();
            demoSettings.FindProperty("_font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MSYH SDF.asset");
            demoSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
        }

        private static Sprite ImportSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
