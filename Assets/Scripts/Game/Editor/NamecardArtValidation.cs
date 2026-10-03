using System.Collections.Generic;
using System.Linq;
using Game.Content;
using Game.Network;
using Game.UI;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class NamecardArtValidation
    {
        public static IEnumerable<string> GetIssues(NamecardArtworkSO artwork)
        {
            if (artwork == null)
            {
                yield return "Missing remote artwork asset.";
                yield break;
            }
            foreach (var issue in GetSpriteIssues(artwork.Wide, false))
                yield return "8:3: " + issue;
            foreach (var issue in GetSpriteIssues(artwork.Compact, true))
                yield return "4:3: " + issue;
        }

        private static IEnumerable<string> GetSpriteIssues(Sprite artwork, bool compact)
        {
            if (artwork == null)
            {
                yield return "Missing artwork.";
                yield break;
            }
            var path = AssetDatabase.GetAssetPath(artwork);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                yield return "Artwork must be an imported texture.";
                yield break;
            }
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            int maxWidth = compact ? NamecardLayout.CompactSourceMaxWidth : NamecardLayout.SourceWidth;
            int maxHeight = compact ? NamecardLayout.CompactSourceMaxHeight : NamecardLayout.SourceHeight;
            int ratioWidth = compact ? 4 : 8;
            if (width > maxWidth || height > maxHeight || width * 3 != height * ratioWidth)
                yield return $"Source {width} x {height}: use {ratioWidth}:3, at most {maxWidth} x {maxHeight}.";
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
                yield return "Use Sprite (2D and UI), Single.";
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
                yield return "Use Full Rect sprite mesh.";
            if (importer.maxTextureSize > NamecardLayout.MaxTextureSize ||
                artwork.texture.width > NamecardLayout.MaxTextureSize ||
                artwork.texture.height > (compact ? NamecardLayout.CompactHeight : 384))
                yield return compact ? "Limit imported texture to 1024 x 768." : "Limit imported texture to 1024 x 384.";
            if (importer.npotScale != TextureImporterNPOTScale.None)
                yield return "Use Non Power of 2 = None to preserve the artwork aspect ratio.";
            foreach (var platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
            {
                var platformSettings = importer.GetPlatformTextureSettings(platform);
                if (platformSettings.overridden && platformSettings.maxTextureSize > NamecardLayout.MaxTextureSize)
                    yield return platform + " overrides the maximum texture size.";
            }
            if (importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp)
                yield return "Disable mipmaps and use Clamp wrapping.";
        }

        [MenuItem("Poker/Shop/Validate Namecard Artwork")]
        public static void ValidateAll()
        {
            var issues = CatalogConfiguration.Load().Namecards.Where(card => !card.IsDefault).SelectMany(card =>
                GetIssues(AssetDatabase.LoadAssetAtPath<NamecardArtworkSO>(RemoteAssets.SourcePath(card.Artwork)))
                    .Select(issue => card.ItemId + ": " + issue)).ToArray();
            if (issues.Length > 0)
                Debug.LogError(string.Join("\n", issues));
            else
                Debug.Log("Namecard texture settings passed. Inspect ornaments against the yellow frame guide in each artwork Inspector.");
        }

        public static void DrawPreview(NamecardArtworkSO artwork)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("名片留白检查", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("黄线：64 px 边框上限；青线：96 px 文字安全区。宽版按 2048 × 768、4:3 按 1024 × 768 换算；亮色装饰和明显光晕留在黄线外。", MessageType.Info);
            foreach (var issue in GetIssues(artwork))
                EditorGUILayout.HelpBox(issue, MessageType.Warning);
            DrawSpritePreview(artwork.Wide, false);
            DrawSpritePreview(artwork.Compact, true);
        }

        private static void DrawSpritePreview(Sprite artwork, bool compact)
        {
            if (artwork == null)
                return;
            int width = compact ? NamecardLayout.CompactWidth : NamecardLayout.SourceWidth;
            int height = compact ? NamecardLayout.CompactHeight : NamecardLayout.SourceHeight;
            EditorGUILayout.LabelField(compact ? "4:3 · 紧凑构图" : "8:3 · 宽版构图");
            var area = GUILayoutUtility.GetAspectRect(width / (float)height);
            GUI.DrawTexture(area, artwork.texture, ScaleMode.ScaleToFit, true);
            Outline(Inset(area, NamecardLayout.FrameInset, width, height), new Color(1f, 0.75f, 0.2f));
            Outline(Inset(area, NamecardLayout.ContentInset, width, height), new Color(0.2f, 0.95f, 1f));
        }

        private static Rect Inset(Rect rect, float pixels, int width, int height)
        {
            float x = rect.width * pixels / width;
            float y = rect.height * pixels / height;
            return new Rect(rect.x + x, rect.y + y, rect.width - 2 * x, rect.height - 2 * y);
        }

        private static void Outline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }
    }
}
