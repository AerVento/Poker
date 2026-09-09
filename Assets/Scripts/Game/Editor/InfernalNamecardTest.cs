using System.Reflection;
using Game.Network.Accounts;
using Game.SO;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class InfernalNamecardTest
    {
        [TestCase(0, "galaxy")]
        [TestCase(99, "galaxy")]
        [TestCase(100, "devil-service")]
        [TestCase(499, "devil-service")]
        [TestCase(500, "archdevil-service")]
        [TestCase(999, "archdevil-service")]
        [TestCase(1000, "satan-service")]
        [TestCase(1001, "satan-service")]
        public void ThresholdIncludesEqualityAndHighestTierWins(int years, string expected)
        {
            Assert.AreEqual(expected, InfernalNamecard.Resolve("galaxy", (ulong)years));
            if (years >= 100)
                Assert.AreEqual(expected, InfernalNamecard.Resolve("default", (ulong)years));
        }

        [Test]
        public void MaximumYearsDoNotOverflow()
        {
            Assert.AreEqual(476741369745UL, InfernalNamecard.CalculateYears(uint.MaxValue, uint.MaxValue, uint.MaxValue));
            Assert.AreEqual("satan-service", InfernalNamecard.Resolve(null, ulong.MaxValue));
            Assert.IsNull(InfernalNamecard.Resolve(null, 0));
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        [TestCase("GamePanel/Player", "_name")]
        [TestCase("RoomPanel/Player", "_playername")]
        [TestCase("LeaderboardPanel/LeaderboardRow", "_name")]
        public void RealRowsOverrideEquipmentAndRestorePooledNameAndMaterial(string path, string nameField)
        {
            var holder = new GameObject("Infernal UI test");
            holder.SetActive(false);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/" + path + ".prefab");
                var row = Object.Instantiate(prefab, holder.transform);
                var appearance = row.GetComponent<NamecardAppearance>();
                if (appearance == null) appearance = Field<NamecardAppearance>(row.GetComponent<LeaderboardRow>(), "_namecard");
                object presenter = path.StartsWith("GamePanel") ? (object)row.GetComponent<Game.UI.GamePanelElement.PlayerListItem>() :
                    path.StartsWith("RoomPanel") ? row.GetComponent<Game.UI.RoomPanelElement.PlayerListItem>() : row.GetComponent<LeaderboardRow>();
                var label = Field<TMP_Text>(presenter, nameField);
                var originalColor = label.color;
                var originalMaterial = label.fontSharedMaterial;
                var originalWidth = originalMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth);
                var catalog = AssetDatabase.LoadAssetAtPath<NamecardCatalogSO>("Assets/Resources/SO/Shop/NamecardCatalog.asset");
                var artwork = Field<Image>(appearance, "_artwork");
                foreach (var years in new ulong[] { 100, 500, 1000 })
                {
                    if (presenter is Game.UI.GamePanelElement.PlayerListItem game) game.SetNamecard("galaxy", years);
                    else if (presenter is Game.UI.RoomPanelElement.PlayerListItem room) room.SetNamecard("galaxy", years);
                    else ((LeaderboardRow)presenter).Show(new LeaderboardEntry { NamecardId = "galaxy", UserName = "Test", ServiceYears = years });
                    var product = catalog.GetProduct(InfernalNamecard.Resolve("galaxy", years));
                    Assert.IsTrue(artwork.sprite == product.Artwork || artwork.sprite == product.Artwork4x3);
                    Assert.AreEqual((Color)new Color32(255, 24, 24, 255), label.color);
                    Assert.AreEqual(Color.black, (Color)label.outlineColor);
                    Assert.AreEqual(0.2f, label.outlineWidth, 0.001f);
                    Assert.AreNotSame(originalMaterial, label.fontSharedMaterial);
                    Assert.AreEqual(originalWidth, originalMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth));
                }
                var material = label.fontSharedMaterial;
                appearance.Present("jade", 1000, label);
                Assert.AreSame(material, label.fontSharedMaterial, "Repeated snapshots must reuse the material.");
                appearance.Present("galaxy", 99, label);
                Assert.AreSame(originalMaterial, label.fontSharedMaterial);
                Assert.AreEqual(originalColor, label.color);
                Assert.IsTrue(material == null, "Temporary material must be released when a row returns to normal.");
            }
            finally { Object.DestroyImmediate(holder); }
        }

        [Test]
        public void ServiceLeaderboardNameRestoresWhenReusedForOrdinaryPlayer()
        {
            var holder = new GameObject("Infernal service row test");
            holder.SetActive(false);
            try
            {
                var row = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/Panels/LeaderboardPanel/ServiceLeaderboardRow.prefab"), holder.transform).GetComponent<ServiceLeaderboardRow>();
                var label = Field<TMP_Text>(row, "_name");
                var color = label.color;
                var material = label.fontSharedMaterial;
                row.Show(new ServiceLeaderboardEntry { SatanContracts = 1, UserName = "Infernal" });
                Assert.AreEqual((Color)new Color32(255, 24, 24, 255), label.color);
                Assert.AreEqual(0.2f, label.outlineWidth, 0.001f);
                row.Show(new ServiceLeaderboardEntry { UserName = "Normal" });
                Assert.AreEqual(color, label.color);
                Assert.AreSame(material, label.fontSharedMaterial);
            }
            finally { Object.DestroyImmediate(holder); }
        }
    }
}