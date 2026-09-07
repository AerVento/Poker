using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class NamecardAppearanceTest
    {
        private GameObject _holder;
        private GameObject _row;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
        }

        private NamecardAppearance Create(string panel)
        {
            _holder = new GameObject("NamecardAppearanceTest");
            _holder.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/UI/Panels/{panel}/Player.prefab");
            _row = Object.Instantiate(prefab, _holder.transform);
            return _row.GetComponent<NamecardAppearance>();
        }

        [TestCase("GamePanel")]
        [TestCase("RoomPanel")]
        public void EquippedArtworkReplacesClassicSurfaceAndKeepsStateIndependent(string panel)
        {
            var appearance = Create(panel);
            appearance.Present("jade");
            var artwork = _row.transform.Find("NamecardMask/NamecardArtwork").GetComponent<Image>();
            var status = _row.transform.Find("NamecardContent/NamecardStateOutline");
            Assert.IsFalse(_row.GetComponent<Image>().enabled, "The green classic surface must not be rendered under custom artwork.");
            Assert.IsTrue(artwork.gameObject.activeSelf);
            Assert.IsNotNull(artwork.sprite);
            appearance.SetHighlighted(true);
            appearance.Present("galaxy");
            Assert.IsTrue(status.gameObject.activeSelf, "A sync must preserve the active turn / ready marker.");
            Assert.IsFalse(_row.GetComponent<Image>().enabled);
            appearance.SetHighlighted(false);
            Assert.IsFalse(status.gameObject.activeSelf);
            Assert.IsTrue(artwork.gameObject.activeSelf);
        }

        [TestCase("GamePanel", "")]
        [TestCase("GamePanel", "default")]
        [TestCase("GamePanel", "missing-style")]
        [TestCase("RoomPanel", "")]
        [TestCase("RoomPanel", "default")]
        [TestCase("RoomPanel", "missing-style")]
        public void SwitchingBackOrUnknownStyleRestoresClassicSurface(string panel, string id)
        {
            var appearance = Create(panel);
            appearance.Present("neon");
            appearance.SetHighlighted(true);
            appearance.Present(id);
            Assert.IsTrue(_row.GetComponent<Image>().enabled);
            Assert.IsFalse(_row.transform.Find("NamecardMask/NamecardArtwork").gameObject.activeSelf);
            Assert.IsTrue(_row.transform.Find("NamecardContent/NamecardStateOutline").gameObject.activeSelf);
        }
    }
}
