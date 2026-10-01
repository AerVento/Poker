using System.Collections.Generic;
using System.Linq;
using Game.Content;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class RemoteContentRulesTests
    {
        private static RemoteContentRecord Record(string address, string format, params string[] urls) => new RemoteContentRecord
        {
            Address = address,
            AssetId = "Assets/RemoteContent/" + address + ".asset",
            AssetProvider = "BundledAssetProvider",
            Format = format,
            Bundles = urls.Select(url => new RemoteBundleRecord { Url = url, Provider = "AssetBundleProvider", Options = "{\"m_Crc\":1}" }).ToList()
        };

        [Test]
        public void JudgesMissingStaleAndCurrent()
        {
            var latest = Record("namecards/galaxy", "f1", "b/galaxy_new.bundle");
            Assert.AreEqual(RemoteContentState.Unavailable, RemoteContentRules.Judge(Record("namecards/galaxy", "f1", "b/x.bundle"), null, "f1"));
            Assert.AreEqual(RemoteContentState.Missing, RemoteContentRules.Judge(null, latest, "f1"));
            Assert.AreEqual(RemoteContentState.Stale, RemoteContentRules.Judge(Record("namecards/galaxy", "f1", "b/galaxy_old.bundle"), latest, "f1"));
            Assert.AreEqual(RemoteContentState.Current, RemoteContentRules.Judge(Record("namecards/galaxy", "f1", "b/galaxy_new.bundle"), latest, "f1"));
        }

        [Test]
        public void RecordOfAnotherFormatIsMissing()
        {
            var latest = Record("gifts/champagne", "f2", "b/new.bundle");
            Assert.AreEqual(RemoteContentState.Missing, RemoteContentRules.Judge(Record("gifts/champagne", "f1", "b/new.bundle"), latest, "f2"));
            Assert.IsFalse(RemoteContentRules.IsUsable(Record("gifts/champagne", "f1", "b/new.bundle"), null));
        }

        [Test]
        public void CrcChangeMakesContentStale()
        {
            var local = Record("cardfaces/minimal", "f", "b/minimal.bundle");
            var latest = Record("cardfaces/minimal", "f", "b/minimal.bundle");
            latest.Bundles[0].Options = "{\"m_Crc\":2}";
            Assert.AreEqual(RemoteContentState.Stale, RemoteContentRules.Judge(local, latest, "f"));
        }

        [Test]
        public void OnlyBundlesNoLongerUsedAreDeleted()
        {
            var old = Record("gifts/gunfire", "f", "b/shared.bundle", "b/old.bundle");
            var next = Record("gifts/gunfire", "f", "b/shared.bundle", "b/new.bundle");
            CollectionAssert.AreEqual(new[] { "b/old.bundle" }, old.BundlesNotIn(next).Select(b => b.Url).ToArray());
            Assert.AreEqual(2, old.BundlesNotIn(null).Count());
        }

        [Test]
        public void ContentVersionRoundTripsAndRejectsUnsafeCatalogNames()
        {
            var version = new RemoteContentVersion("20261001T063000Z", "abc", "catalog_v1.json");
            Assert.IsTrue(RemoteContentVersion.TryParse(version.ToString().Replace("\n", "\r\n"), out var parsed));
            Assert.AreEqual("20261001T063000Z", parsed.Version);
            Assert.AreEqual("abc", parsed.Format);
            Assert.AreEqual("catalog_v1.json", parsed.Catalog);
            Assert.IsFalse(RemoteContentVersion.TryParse("version=1\nformat=a\ncatalog=../x.json", out _));
            Assert.IsFalse(RemoteContentVersion.TryParse("version=1\nformat=a", out _));
            Assert.IsFalse(RemoteContentVersion.TryParse("<html>404</html>", out _));
        }

        [Test]
        public void SwitchesOnlyToNewerContentOfThePageFormat()
        {
            var current = new RemoteContentVersion("1", "f", "c.json");
            Assert.IsFalse(RemoteContentRules.ShouldSwitch(current, new RemoteContentVersion("1", "f", "c.json"), "f"));
            Assert.IsTrue(RemoteContentRules.ShouldSwitch(current, new RemoteContentVersion("2", "f", "c.json"), "f"));
            Assert.IsFalse(RemoteContentRules.ShouldSwitch(current, new RemoteContentVersion("2", "g", "c.json"), "f"));
            Assert.IsFalse(RemoteContentRules.Accepts(new RemoteContentVersion("2", "f", "c.json"), ""));
        }

        [Test]
        public void RecordBookKeepsOneVersionPerAddressAndSurvivesBadData()
        {
            var book = new RemoteContentRecordBook();
            book.Set(Record("namecards/galaxy", "f", "b/old.bundle"));
            book.Set(Record("namecards/galaxy", "f", "b/new.bundle"));
            book.Set(Record("namecards/jade", "f", "b/jade.bundle"));
            var restored = RemoteContentRecordBook.Parse(book.Serialize());
            Assert.AreEqual(2, restored.All.Count());
            Assert.AreEqual("b/new.bundle", restored.Get("namecards/galaxy").Bundles.Single().Url);
            Assert.IsTrue(restored.Remove("namecards/jade"));
            Assert.IsNull(restored.Get("namecards/jade"));
            Assert.AreEqual(0, RemoteContentRecordBook.Parse("not json").All.Count());
            Assert.AreEqual(0, RemoteContentRecordBook.Parse("{\"Records\":[{\"Address\":\"x\",\"Bundles\":[]}]}").All.Count());
        }

        [Test]
        public void CatalogProducesLoadableRecords()
        {
            var path = "Build/RemoteContent/Content/v1/StandaloneWindows64/catalog_v1.json";
            if (!System.IO.File.Exists(path)) Assert.Ignore("No Windows content build.");
            var catalog = new RemoteContentCatalog(new RemoteContentVersion("1", "f", "catalog_v1.json"), System.IO.File.ReadAllText(path));
            var record = catalog.Find("namecards/galaxy");
            Assert.NotNull(record);
            Assert.AreEqual("f", record.Format);
            Assert.AreEqual("Assets/RemoteContent/namecards/Galaxy.asset", record.AssetId);
            var bundle = record.Bundles.Single();
            StringAssert.StartsWith(RemoteAssets.BuildOrigin + "/StandaloneWindows64/namecard_galaxy", bundle.Url);
            Assert.NotZero(RemoteContentCatalog.Options(bundle).Crc);
            Assert.IsNull(catalog.Find("namecards/unknown"));
            var location = RemoteContentCatalog.ToLocation(record, typeof(NamecardArtworkSO));
            Assert.AreEqual(record.AssetId, location.InternalId);
            Assert.AreEqual(bundle.Url, location.Dependencies.Single().InternalId);
            Assert.AreEqual(RemoteContentCatalog.Options(bundle).Hash,
                ((UnityEngine.ResourceManagement.ResourceProviders.AssetBundleRequestOptions)location.Dependencies.Single().Data).Hash);
            Assert.IsTrue(record.SameContent(RemoteContentCatalog.ToRecord("namecards/galaxy", location, "f")));
        }

        [Test]
        public void ClientBuildStampParses()
        {
            var values = RemoteContentVersion.ParseLines(ClientBuild.Format("20261001T000000Z", "abc"));
            Assert.AreEqual("20261001T000000Z", values["page"]);
            Assert.AreEqual("abc", values["format"]);
        }

        [Test]
        public void ContentFormatIsStableAndCoversRemoteScripts()
        {
            var lines = Game.Editor.ContentFormatVersion.Describe().ToList();
            Assert.AreEqual(Game.Editor.ContentFormatVersion.Compute(), Game.Editor.ContentFormatVersion.Compute());
            Assert.IsTrue(lines.Any(line => line == "type:Game.Content.NamecardArtworkSO"));
            Assert.IsTrue(lines.Any(line => line.StartsWith("field:Game.Content.NamecardArtworkSO._wide:")));
            Assert.IsTrue(lines.Any(line => line.StartsWith("unity:")));
            Assert.AreNotEqual(Game.Editor.ContentFormatVersion.Hash(lines), Game.Editor.ContentFormatVersion.Hash(lines.Concat(new[] { "field:x" })));
        }
    }
}
