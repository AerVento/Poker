using System.Linq;
using System.Reflection;
using Game.Editor;
using Game.SO;
using Game.UI;
using Game.UI.GamePanelElement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class NamecardArtworkVariantTest
    {
        private GameObject _holder;
        private NamecardProductSO _copy;
        private static NamecardCatalogSO Catalog => Resources.Load<NamecardCatalogSO>(NamecardCatalogSO.ResourcePath);

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
            Object.DestroyImmediate(_copy);
        }

        [TestCase("galaxy")]
        [TestCase("ember")]
        [TestCase("neon")]
        [TestCase("gilded")]
        [TestCase("frost")]
        [TestCase("abyss")]
        [TestCase("jade")]
        [TestCase("sakura")]
        [TestCase("clockwork")]
        [TestCase("eclipse")]
        [TestCase("prism")]
        public void EverySkinResolvesItsOwnDistinctArtForBothAspectRatios(string id)
        {
            var product = Catalog.GetProduct(id);
            Assert.IsNotNull(product);
            Assert.AreEqual(1, Catalog.Products.Count(p => p != null && p.Id == id));
            Assert.AreSame(product.Artwork, Catalog.GetArtwork(id, 8f / 3f));
            Assert.AreSame(product.Artwork4x3, Catalog.GetArtwork(id, 4f / 3f));
            Assert.AreNotSame(product.Artwork, product.Artwork4x3);
            Assert.AreEqual($"Assets/Sprites/Namecards/{id}-v2.png", AssetDatabase.GetAssetPath(product.Artwork));
            Assert.AreEqual($"Assets/Sprites/Namecards/{id}-4x3-v1.png", AssetDatabase.GetAssetPath(product.Artwork4x3));
            Assert.AreEqual(1024, product.Artwork4x3.texture.width);
            Assert.AreEqual(768, product.Artwork4x3.texture.height);
            Assert.IsEmpty(NamecardArtValidation.GetIssues(product));
        }

        [Test]
        public void MissingVariantFallsBackWithoutChangingInventoryIdentity()
        {
            _copy = Object.Instantiate(Catalog.GetProduct("neon"));
            var wide = _copy.Artwork;
            var compact = _copy.Artwork4x3;
            var serialized = new SerializedObject(_copy);
            serialized.FindProperty("_artwork4x3").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreSame(wide, _copy.GetArtwork(4f / 3f));
            serialized.FindProperty("_artwork4x3").objectReferenceValue = compact;
            serialized.FindProperty("_artwork").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreSame(compact, _copy.GetArtwork(8f / 3f));
            Assert.AreEqual("neon", _copy.Id);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void UnmeasuredSlotUsesWideFallback(float aspect)
        {
            Assert.AreSame(Catalog.GetProduct("jade").Artwork, Catalog.GetArtwork("jade", aspect));
        }

        [Test]
        public void DefaultAndUnknownIdsKeepTheirExistingMeaning()
        {
            Assert.IsTrue(Catalog.GetProduct(null).IsDefault);
            Assert.AreSame(Catalog.GetProduct("default"), Catalog.GetProduct(""));
            Assert.AreEqual(0, Catalog.GetProduct("default").Price);
            Assert.IsNull(Catalog.GetProduct("missing-style"));
            Assert.IsNull(Catalog.GetArtwork("missing-style", 4f / 3f));
        }

        [TestCase("Player4", false)]
        [TestCase("Player8", true)]
        [TestCase("Player16", false)]
        public void ExistingPlayerIdInterfaceChoosesArtAndKeepsSlotSize(string prefabName, bool compact)
        {
            var appearance = Create(prefabName);
            var rect = (RectTransform)appearance.transform;
            var size = rect.rect.size;
            appearance.GetComponent<PlayerListItem>().SetNamecard(Catalog.GetProduct("sakura"));
            var artwork = Artwork(appearance);
            Assert.AreSame(compact ? Catalog.GetProduct("sakura").Artwork4x3 : Catalog.GetProduct("sakura").Artwork, artwork.sprite);
            Assert.AreEqual(size, rect.rect.size, "Art must not resize the player layout.");
            Assert.IsTrue(artwork.preserveAspect);
            Assert.IsFalse(appearance.GetComponent<Image>().enabled);
        }

        [Test]
        public void ResizingSlotReSelectsArtAndMovesAllContentInsideItsSafeArea()
        {
            var appearance = Create("Player8");
            var rect = (RectTransform)appearance.transform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1024f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 768f);
            appearance.Present("galaxy");
            appearance.SetHighlighted(true);
            var artwork = Artwork(appearance);
            var content = (RectTransform)appearance.transform.Find("NamecardContent");
            Assert.AreSame(Catalog.GetProduct("galaxy").Artwork4x3, artwork.sprite);
            Assert.That(Vector2.Distance(NamecardLayout.CompactSafeMin, content.anchorMin), Is.LessThan(0.001f));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 2048f);
            typeof(NamecardAppearance).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(appearance, null);
            Assert.AreSame(Catalog.GetProduct("galaxy").Artwork, artwork.sprite);
            Assert.That(Vector2.Distance(NamecardLayout.SafeMin, content.anchorMin), Is.LessThan(0.001f));
            Assert.IsTrue(content.Find("StatusSlot/NamecardStateOutline").gameObject.activeSelf);
            appearance.Present("default");
            Assert.IsFalse(artwork.gameObject.activeSelf);
            Assert.IsTrue(appearance.GetComponent<Image>().enabled);
        }

        private NamecardAppearance Create(string prefabName)
        {
            _holder = new GameObject("NamecardArtworkVariantTest");
            _holder.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/UI/Panels/GamePanel/{prefabName}.prefab");
            return Object.Instantiate(prefab, _holder.transform).GetComponent<NamecardAppearance>();
        }

        private static Image Artwork(NamecardAppearance appearance)
        {
            return (Image)new SerializedObject(appearance).FindProperty("_artwork").objectReferenceValue;
        }
    }
}
