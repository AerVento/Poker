using System.IO;
using Game.Content;
using Game.Network;
using Game.SO;
using UnityEditor;
using UnityEngine;

namespace Game.UI.Editor
{
    public static class CardFacePreviewBuilder
    {
        public const string DirectoryPath = "Assets/Sprites/CardFaces/Previews";

        // Previews of every configured face, named by its Preview field. The classic one stays local (ShopLocalArt);
        // the others go into the shared remote preview set on the next Prepare.
        [MenuItem("Poker/Card Faces/Generate Previews")]
        public static void Generate()
        {
            Directory.CreateDirectory(DirectoryPath);
            foreach (var face in CatalogConfiguration.Load().CardFaces)
            {
                var cards = face.IsDefault ? ShopLocalArtSO.Instance?.ClassicCards :
                    AssetDatabase.LoadAssetAtPath<PokerCardsSO>(RemoteAssets.SourcePath(face.Cards));
                if (cards == null) throw new System.InvalidOperationException("Missing cards: " + face.ItemId);
                var target = RenderTexture.GetTemporary(384, 256, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                var image = new Texture2D(384, 256, TextureFormat.RGBA32, false);
                try
                {
                    RenderTexture.active = target;
                    GL.Clear(true, true, Color.clear);
                    GL.PushMatrix();
                    try
                    {
                        GL.LoadPixelMatrix(0, 384, 256, 0);
                        Draw(cards.GetSprite(new Poker(PokerSuit.Spade, PokerRank.Ace)), new Rect(23, 16, 159, 224));
                        Draw(cards.Back, new Rect(202, 16, 159, 224));
                    }
                    finally { GL.PopMatrix(); }
                    image.ReadPixels(new Rect(0, 0, 384, 256), 0, 0);
                    image.Apply();
                    var path = DirectoryPath + "/" + face.Preview + ".png";
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.isReadable = false;
                    importer.maxTextureSize = 512;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(target);
                    Object.DestroyImmediate(image);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static void Draw(Sprite sprite, Rect destination)
        {
            var texture = sprite.texture;
            var rect = sprite.rect;
            var uv = new Rect(rect.x / texture.width, rect.y / texture.height,
                rect.width / texture.width, rect.height / texture.height);
            Graphics.DrawTexture(destination, texture, uv, 0, 0, 0, 0);
        }
    }
}
