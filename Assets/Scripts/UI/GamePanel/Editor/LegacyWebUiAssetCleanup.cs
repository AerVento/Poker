using System;
using UnityEditor;
using UnityEngine;

namespace Game.UI.EditorTools
{
    public static class LegacyWebUiAssetCleanup
    {
        private static readonly string[] LegacyAssets =
        {
            "Assets/WebGLTemplates/Aervento/navigation.css",
            "Assets/WebGLTemplates/Aervento/navigation.js",
            "Assets/WebGLTemplates/Aervento/navigation.json",
            "Assets/WebGLTemplates/Aervento/TemplateData/fullscreen-button.png",
            "Assets/WebGLTemplates/Aervento/TemplateData/webgl-logo.png",
        };

        [MenuItem("Poker/WebGL/Remove Legacy Navigation Assets")]
        public static void Remove()
        {
            foreach (var path in LegacyAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null && !AssetDatabase.DeleteAsset(path))
                    throw new InvalidOperationException("Could not delete legacy WebGL asset: " + path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Removed five unused WebGL navigation and branding assets.");
        }
    }
}
