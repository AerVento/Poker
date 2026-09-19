#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.EditorTools
{
    public static class AchievementHiddenAndBatchMigration
    {
        private const string PanelPath = "Assets/Prefabs/UI/Panels/AchievementPanel/AchievementPanel.prefab";
        private static readonly string[] IconPaths =
        {
            "Assets/Resources/UI/Achievements/Hidden-v1.png",
            "Assets/Resources/UI/Achievements/Ashes-v1.png"
        };

        [MenuItem("Poker/UI/Migrate Hidden Achievements And Batch Claim")]
        public static void Run()
        {
            ConfigureIcons();
            ConfigurePanel();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Hidden achievement icons and batch claim button migrated.");
        }

        private static void ConfigureIcons()
        {
            foreach (var path in IconPaths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                    throw new MissingReferenceException("Achievement icon is missing: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigurePanel()
        {
            var root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var panel = root.GetComponent<AchievementPanel>() ??
                    throw new MissingComponentException("AchievementPanel component is missing.");
                var serialized = new SerializedObject(panel);
                var claimAllProperty = serialized.FindProperty("_claimAll");
                var claimAllLabelProperty = serialized.FindProperty("_claimAllLabel");
                var template = serialized.FindProperty("_tabTemplate")?.objectReferenceValue as Button;
                var layout = serialized.FindProperty("_layout")?.objectReferenceValue as RectTransform;
                if (claimAllProperty == null || claimAllLabelProperty == null || template == null || layout == null)
                    throw new MissingReferenceException("AchievementPanel migration fields are incomplete.");

                var existing = layout.Find("ClaimAll");
                var button = existing != null ? existing.GetComponent<Button>() : null;
                if (button == null)
                {
                    var clone = Object.Instantiate(template.gameObject, layout, false);
                    clone.name = "ClaimAll";
                    clone.SetActive(true);
                    button = clone.GetComponent<Button>();
                }

                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(1120, -857);
                rect.sizeDelta = new Vector2(360, 58);
                var feedback = layout.Find("Feedback") as RectTransform;
                if (feedback != null) feedback.sizeDelta = new Vector2(840, feedback.sizeDelta.y);
                var image = button.GetComponent<Image>();
                if (image != null) image.color = new Color32(217, 188, 122, 255);
                var label = button.GetComponentInChildren<TextMeshProUGUI>(true) ??
                    throw new MissingComponentException("ClaimAll label is missing.");
                label.text = "一键领取";
                label.fontSize = 26;
                label.color = new Color32(18, 35, 31, 255);
                label.raycastTarget = false;

                claimAllProperty.objectReferenceValue = button;
                claimAllLabelProperty.objectReferenceValue = label;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
