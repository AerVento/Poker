using System;
using System.Collections.Generic;
using System.Linq;
using Game.Content;
using Game.UI.Editor;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class RemoteContentDependencyTests
    {
        // A version of a package: its version string and "address@requirement" dependencies.
        private static RemoteContentRecord R(string address, string version, params string[] dependencies) => new RemoteContentRecord
        {
            Address = address,
            Version = version ?? string.Empty,
            Dependencies = dependencies.Select(d => d.Split('@')).Select(p => new RemoteContentDependency(p[0], p[1])).ToList()
        };

        private sealed class World
        {
            public readonly Dictionary<string, RemoteContentRecord> Local = new Dictionary<string, RemoteContentRecord>();
            public readonly Dictionary<string, RemoteContentRecord> Latest = new Dictionary<string, RemoteContentRecord>();

            public World Have(RemoteContentRecord record) { Local[record.Address] = record; return this; }
            public World Site(RemoteContentRecord record) { Latest[record.Address] = record; return this; }
            // Current: the local version is the latest one.
            public World Both(RemoteContentRecord record) => Have(record).Site(record);

            private RemoteContentRecord FindLocal(string a) => Local.TryGetValue(a, out var r) ? r : null;
            private RemoteContentRecord FindLatest(string a) => Latest.TryGetValue(a, out var r) ? r : null;

            public RemoteContentPlan Use(string root) => RemoteContentPlanner.ForUse(root, FindLocal, FindLatest);
            public RemoteContentPlan Update(string target) => RemoteContentPlanner.ForUpdate(target, Local.Keys, FindLocal, FindLatest);
        }

        private static void AssertUpdates(RemoteContentPlan plan, params string[] expected)
        {
            Assert.IsTrue(plan.Success, plan.Error);
            CollectionAssert.AreEquivalent(expected, plan.Updates);
        }

        [Test]
        public void ParsesVersionsAndRequirements()
        {
            Assert.IsTrue(SemanticVersion.TryParse("1.12.3", out var version));
            Assert.AreEqual("1.12.3", version.ToString());
            foreach (var bad in new[] { null, "", "1.2", "1.2.3.4", "1.-2.3", "v1.2.3", "1. 2.3", "1..3" })
                Assert.IsFalse(SemanticVersion.TryParse(bad, out _), bad);
            Assert.IsTrue(SemanticVersion.TryParseRequirement("1.2", out _));
            Assert.IsFalse(SemanticVersion.TryParseRequirement("1.2.0", out _));
            Assert.IsTrue(SemanticVersion.Satisfies("1.2.0", "1.2"));
            Assert.IsTrue(SemanticVersion.Satisfies("1.7.5", "1.2"));
            Assert.IsFalse(SemanticVersion.Satisfies("1.1.9", "1.2"));
            Assert.IsFalse(SemanticVersion.Satisfies("2.0.0", "1.2"));
            Assert.IsFalse(SemanticVersion.Satisfies(null, "1.0"));
            Assert.IsFalse(SemanticVersion.Satisfies("1.0.0", "x"));
        }

        // The nine combinations of a gift X and the shared package D it depends on.
        [Test]
        public void SingleDependency_BothCurrent_UsesLocal() =>
            AssertUpdates(new World().Both(R("X", null, "D@1.1")).Both(R("D", "1.1.0")).Use("X"));

        [Test]
        public void SingleDependency_StaleDependency()
        {
            // Compatible: the old D is used, nothing waits.
            AssertUpdates(new World().Both(R("X", null, "D@1.1")).Have(R("D", "1.1.0")).Site(R("D", "1.1.1")).Use("X"));
            // X uses something newer than the local D.
            AssertUpdates(new World().Both(R("X", null, "D@1.1")).Have(R("D", "1.0.0")).Site(R("D", "1.1.0")).Use("X"), "D");
        }

        [Test]
        public void SingleDependency_MissingDependency() =>
            AssertUpdates(new World().Both(R("X", null, "D@1.1")).Site(R("D", "1.1.0")).Use("X"), "D");

        [Test]
        public void SingleDependency_StaleGift()
        {
            AssertUpdates(new World().Have(R("X", null, "D@1.0")).Site(R("X", null, "D@1.1")).Both(R("D", "1.1.0")).Use("X"));
            // D moved to a new major version: the old X cannot use it.
            AssertUpdates(new World().Have(R("X", null, "D@1.0")).Site(R("X", null, "D@2.0")).Both(R("D", "2.0.0")).Use("X"), "X");
        }

        [Test]
        public void SingleDependency_BothStale()
        {
            AssertUpdates(new World().Have(R("X", null, "D@1.0")).Site(R("X", null, "D@2.0"))
                .Have(R("D", "1.0.0")).Site(R("D", "2.0.0")).Use("X"));
            AssertUpdates(new World().Have(R("X", null, "D@1.2")).Site(R("X", null, "D@1.3"))
                .Have(R("D", "1.1.0")).Site(R("D", "1.3.0")).Use("X"), "D");
            AssertUpdates(new World().Have(R("X", null, "D@1.2")).Site(R("X", null, "D@2.0"))
                .Have(R("D", "1.1.0")).Site(R("D", "2.0.0")).Use("X"), "X", "D");
        }

        [Test]
        public void SingleDependency_StaleGiftMissingDependency()
        {
            AssertUpdates(new World().Have(R("X", null, "D@1.0")).Site(R("X", null, "D@1.1")).Site(R("D", "1.1.0")).Use("X"), "D");
            AssertUpdates(new World().Have(R("X", null, "D@1.0")).Site(R("X", null, "D@2.0")).Site(R("D", "2.0.0")).Use("X"), "D", "X");
        }

        [Test]
        public void SingleDependency_MissingGift()
        {
            AssertUpdates(new World().Site(R("X", null, "D@1.1")).Both(R("D", "1.1.0")).Use("X"), "X");
            AssertUpdates(new World().Site(R("X", null, "D@1.1")).Have(R("D", "1.1.0")).Site(R("D", "1.2.0")).Use("X"), "X");
            AssertUpdates(new World().Site(R("X", null, "D@1.2")).Have(R("D", "1.1.0")).Site(R("D", "1.2.0")).Use("X"), "X", "D");
            AssertUpdates(new World().Site(R("X", null, "D@1.1")).Site(R("D", "1.1.0")).Use("X"), "X", "D");
        }

        [Test]
        public void MultipleDependencies_ReplanWithLatestDeclarations()
        {
            // W moved to 2.0, so X must update; the latest X also needs a newer Y.
            var world = new World()
                .Have(R("X", null, "Y@1.0", "W@1.0")).Site(R("X", null, "Y@1.4", "W@2.0"))
                .Have(R("Y", "1.0.0")).Site(R("Y", "1.4.0"))
                .Both(R("W", "2.0.0"));
            var plan = world.Use("X");
            AssertUpdates(plan, "X", "Y");
            CollectionAssert.AreEquivalent(new[] { "X", "Y", "W" }, plan.Closure);
        }

        [Test]
        public void DiamondDependency_SharedPackageHasOneVersion()
        {
            AssertUpdates(new World().Both(R("X", null, "Y@1.0", "W@1.0")).Both(R("Y", "1.0.0", "Z@1.2")).Both(R("W", "1.0.0", "Z@1.4"))
                .Have(R("Z", "1.3.0")).Site(R("Z", "1.5.0")).Use("X"), "Z");
            // The latest Z is 2.0: W cannot get its 1.4 from it, so W updates, and then Y too.
            AssertUpdates(new World().Both(R("X", null, "Y@1.0", "W@1.0"))
                .Have(R("Y", "1.0.0", "Z@1.2")).Site(R("Y", "1.1.0", "Z@2.0"))
                .Have(R("W", "1.0.0", "Z@1.4")).Site(R("W", "1.1.0", "Z@2.0"))
                .Have(R("Z", "1.3.0")).Site(R("Z", "2.0.0")).Use("X"), "W", "Z", "Y");
        }

        [Test]
        public void ChainDependency_PropagatesDownAndUp()
        {
            // All stale but compatible: the local chain is used.
            var world = new World()
                .Have(R("X", null, "Y@1.0")).Site(R("X", null, "Y@1.1"))
                .Have(R("Y", "1.0.0", "Z@1.0")).Site(R("Y", "1.1.0", "Z@2.0"))
                .Have(R("Z", "1.0.0")).Site(R("Z", "2.0.0"));
            var plan = world.Use("X");
            AssertUpdates(plan);
            CollectionAssert.AreEquivalent(new[] { "X", "Y", "Z" }, plan.Closure);
            // Without a local Z: the latest Z (2.0) breaks the local Y, the latest Y still suits the local X.
            world.Local.Remove("Z");
            AssertUpdates(world.Use("X"), "Z", "Y");
            // If the latest Y is also a new major version, the update reaches X.
            world.Site(R("Y", "2.0.0", "Z@2.0")).Site(R("X", null, "Y@2.0"));
            AssertUpdates(world.Use("X"), "Z", "Y", "X");
        }

        // Z left the catalog and its record was purged: the old X moves to its latest version, which no longer needs Z.
        [Test]
        public void RemovedDependency_UpdatesTheDependent() =>
            AssertUpdates(new World().Have(R("X", null, "Z@1.0")).Site(R("X", null)).Use("X"), "X");

        [Test]
        public void MixedRecordCycle_MovesTheCycleToLatest()
        {
            // An old Y depended on Z; the new Z depends on Y instead.
            var world = new World()
                .Have(R("Y", "1.0.0", "Z@1.0")).Site(R("Y", "1.1.0"))
                .Have(R("Z", "1.0.0", "Y@1.0")).Site(R("Z", "1.1.0", "Y@1.1"));
            AssertUpdates(world.Use("Y"), "Y", "Z");
        }

        [Test]
        public void InconsistentCatalogOrUnknownRootFails()
        {
            Assert.IsFalse(new World().Site(R("X", null, "D@2.0")).Site(R("D", "1.0.0")).Use("X").Success);
            Assert.IsFalse(new World().Use("X").Success);
            // A latest cycle can only come from a broken build.
            Assert.IsFalse(new World().Site(R("Y", "1.0.0", "Z@1.0")).Site(R("Z", "1.0.0", "Y@1.0")).Use("Y").Success);
        }

        [Test]
        public void PackageWithoutDependenciesIsUnaffected()
        {
            AssertUpdates(new World().Have(R("N", null)).Site(R("N", null)).Use("N"));
            AssertUpdates(new World().Site(R("N", null)).Use("N"), "N");
        }

        [Test]
        public void BackgroundUpdateKeepsAllRecordsCompatible()
        {
            // D goes to 2.0 and now needs E: E comes along, the local X and Y (both need D 1.0) would break and update too,
            // W without dependencies is untouched.
            var world = new World()
                .Have(R("D", "1.0.0")).Site(R("D", "2.0.0", "E@1.0"))
                .Have(R("X", null, "D@1.0")).Site(R("X", null, "D@2.0"))
                .Have(R("Y", null, "D@1.0")).Site(R("Y", null, "D@2.0"))
                .Both(R("W", null))
                .Site(R("E", "1.0.0"));
            AssertUpdates(world.Update("D"), "D", "E", "X", "Y");
            // A minor update breaks nobody.
            var minor = new World().Have(R("D", "1.0.0")).Site(R("D", "1.1.0")).Both(R("X", null, "D@1.0"));
            AssertUpdates(minor.Update("D"), "D");
            Assert.IsFalse(new World().Have(R("D", "1.0.0")).Update("D").Success);
        }

        [Test]
        public void VersionFileCarriesThePackageManifest()
        {
            var version = new RemoteContentVersion("1", "f", "catalog_v1.json", RemoteContentManifest.FileName);
            Assert.IsTrue(RemoteContentVersion.TryParse(version.ToString(), out var parsed));
            Assert.AreEqual(RemoteContentManifest.FileName, parsed.Packages);
            Assert.IsTrue(RemoteContentVersion.TryParse("version=1\nformat=f\ncatalog=c.json", out var old));
            Assert.AreEqual(string.Empty, old.Packages);
            Assert.IsFalse(RemoteContentVersion.TryParse("version=1\nformat=f\ncatalog=c.json\npackages=../p.json", out _));
            var manifest = RemoteContentManifest.Parse("{\"Packages\":[{\"Address\":\"lua/shared\",\"Version\":\"1.0.0\",\"Dependencies\":[]}," +
                "{\"Address\":\"gifts/x\",\"Version\":\"\",\"Dependencies\":[{\"_address\":\"lua/shared\",\"_version\":\"1.0\"}]}]}");
            Assert.AreEqual("1.0", manifest.Find("gifts/x").Dependencies[0].Version);
            Assert.AreEqual(0, RemoteContentManifest.Parse(null).Packages.Count);
        }

        [Test]
        public void RecordBookKeepsVersionAndDependencies()
        {
            var book = new RemoteContentRecordBook();
            var record = R("gifts/x", null, "lua/shared@1.0");
            record.Bundles.Add(new RemoteBundleRecord { Url = "b/x.bundle" });
            book.Set(record);
            var restored = RemoteContentRecordBook.Parse(book.Serialize()).Get("gifts/x");
            Assert.AreEqual("lua/shared", restored.Dependencies.Single().Address);
            Assert.AreEqual("1.0", restored.Dependencies.Single().Version);
        }

        private static RemotePackageLock.Package L(string version, string hash, params string[] exports) =>
            new RemotePackageLock.Package { Address = "lua/shared", Version = version, Hash = hash, Exports = exports.ToList() };

        [Test]
        public void LockRequiresMatchingVersionChanges()
        {
            var locked = L("1.3.0", "h", "M", "M.a");
            Assert.IsNull(RemotePackageLock.Compare(null, L("1.0.0", "x")));
            Assert.IsNull(RemotePackageLock.Compare(locked, L("1.3.0", "h", "M", "M.a")));
            StringAssert.Contains("1.3.1", RemotePackageLock.Compare(locked, L("1.3.0", "h2", "M", "M.a")));
            Assert.IsNull(RemotePackageLock.Compare(locked, L("1.3.1", "h2", "M", "M.a")));
            StringAssert.Contains("1.4.0", RemotePackageLock.Compare(locked, L("1.3.1", "h2", "M", "M.a", "M.b")));
            Assert.IsNull(RemotePackageLock.Compare(locked, L("1.4.0", "h2", "M", "M.a", "M.b")));
            StringAssert.Contains("2.0.0", RemotePackageLock.Compare(locked, L("1.9.0", "h2", "M")));
            StringAssert.Contains("2.0.0", RemotePackageLock.Compare(locked, L("1.4.0", "h2", "N", "N.a")));
            Assert.IsNull(RemotePackageLock.Compare(locked, L("2.0.0", "h2", "M")));
            StringAssert.Contains("lower", RemotePackageLock.Compare(locked, L("1.2.9", "h", "M", "M.a")));
            StringAssert.Contains("invalid", RemotePackageLock.Compare(locked, L("1.3", "h", "M", "M.a")));
        }
    }
}
