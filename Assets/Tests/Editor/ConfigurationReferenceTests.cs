using System;
using System.IO;
using System.Linq;
using Framework.Configs;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ConfigurationReferenceTests
    {
        private string _directory;
        private ConfigurationReferenceCatalog _catalog;
        [SetUp]
        public void SetUp()
        {
            _directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/ConfigurationReferenceTests/" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Path.Combine(_directory, "Items"));
            Directory.CreateDirectory(Path.Combine(_directory, "DailyTasks"));
            _catalog = new ConfigurationReferenceCatalog(_directory);
        }
        [TearDown]
        public void TearDown() => Directory.Delete(_directory, true);

        [Test]
        public void ReferencesHaveAttributesButDefinitionsRemainFreeText()
        {
            foreach (var type in new[] { typeof(RewardConfigEntry), typeof(ShopConfigEntry), typeof(NamecardConfigEntry),
                typeof(CardFaceConfigEntry), typeof(GiftConfigEntry), typeof(ContractConfigEntry), typeof(ServiceNamecardConfigEntry) })
                Assert.AreEqual(ConfigReferenceKind.Item, ConfigurationMetadata.Attribute<ConfigReferenceAttribute>(
                    ConfigurationMetadata.Members(type).Single(m => m.UnderlyingName.EndsWith("ItemId", StringComparison.Ordinal))).Kind);
            foreach (var type in new[] { typeof(FixedTaskConfigEntry), typeof(RandomTaskConfigEntry) })
                Assert.AreEqual(ConfigReferenceKind.Task, ConfigurationMetadata.Attribute<ConfigReferenceAttribute>(
                    ConfigurationMetadata.Members(type).Single(m => m.UnderlyingName == "TaskId")).Kind);
            foreach (var type in new[] { typeof(ItemConfigEntry), typeof(TaskConfigEntry) })
                Assert.IsNull(ConfigurationMetadata.Attribute<ConfigReferenceAttribute>(ConfigurationMetadata.Members(type).Single(m => m.UnderlyingName == "Id")));
        }

        [Test]
        public void SuggestionsUseCurrentFilesAndSearchNamesOrIds()
        {
            WriteItems(new ItemConfigEntry { Id = "rotten-egg", Name = "臭鸡蛋" }, new ItemConfigEntry { Id = "cigar", Name = "雪茄" });
            var snapshot = _catalog.Get(ConfigReferenceKind.Item);
            Assert.IsNull(snapshot.Error);
            Assert.AreEqual("rotten-egg", snapshot.Search("EGG").Single().Id);
            Assert.AreEqual("cigar", snapshot.Search("雪茄").Single().Id);
            Assert.IsTrue(snapshot.Contains("cigar"));
            Assert.IsFalse(snapshot.Contains("CIGAR"));
            Assert.IsFalse(snapshot.Contains("missing"));
            Assert.IsEmpty(snapshot.Search("nothing"));
            WriteItems(new ItemConfigEntry { Id = "new-item", Name = "新增物品" });
            snapshot = _catalog.Get(ConfigReferenceKind.Item, true);
            Assert.IsFalse(snapshot.Contains("cigar"));
            Assert.IsTrue(snapshot.Contains("new-item"));
        }

        [Test]
        public void MissingOrBrokenSourceClearsStaleSuggestionsAndCanRecover()
        {
            Assert.IsNotNull(_catalog.Get(ConfigReferenceKind.Item).Error);
            WriteItems(new ItemConfigEntry { Id = "egg", Name = "鸡蛋" });
            Assert.IsTrue(_catalog.Get(ConfigReferenceKind.Item, true).Contains("egg"));
            File.WriteAllText(Path.Combine(_directory, "Items/Items.json"), "broken json");
            var failed = _catalog.Get(ConfigReferenceKind.Item, true);
            Assert.IsNotNull(failed.Error);
            Assert.IsEmpty(failed.Options);
            WriteItems(new ItemConfigEntry { Id = "restored" });
            Assert.IsTrue(_catalog.Get(ConfigReferenceKind.Item, true).Contains("restored"));
        }

        [Test]
        public void TaskCandidatesAreIndependentAndAttributesDoNotValidateInput()
        {
            var tasks = new TaskDefinitionsConfigData { new TaskConfigEntry { Id = "daily", Name = "每日补给" } };
            File.WriteAllText(Path.Combine(_directory, "DailyTasks/TaskDefinitions.json"), ConfigManager.Serialize(tasks));
            var snapshot = _catalog.Get(ConfigReferenceKind.Task);
            Assert.AreEqual("daily", snapshot.Search("补给").Single().Id);
            var data = new FixedTaskConfigEntry { TaskId = "not-in-catalog" };
            Assert.AreEqual(data.TaskId, ConfigManager.Deserialize<FixedTaskConfigEntry>(ConfigManager.Serialize(data)).TaskId);
        }

        private void WriteItems(params ItemConfigEntry[] entries)
        {
            var items = new ItemsConfigData();
            items.AddRange(entries);
            File.WriteAllText(Path.Combine(_directory, "Items/Items.json"), ConfigManager.Serialize(items));
        }
    }
}
