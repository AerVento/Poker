using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Framework.Configs;
using Game.Network;
using Game.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ConfigurationMetadataTests
    {
        public sealed class NestedData
        {
            [Header("分组"), Tooltip("字段说明"), Space(8), JsonProperty("renamed")]
            public int Value = 42;
            [JsonIgnore] public string Ignored = "hidden";
            public bool Enabled = true;
            public List<int> Values = new List<int> { 7, 8 };
            public Dictionary<string, List<int>> Lookup = new Dictionary<string, List<int>>();
        }

        public sealed class FieldDefaults
        {
            [JsonProperty(Required = Required.Always)] public string RequiredText;
            [JsonProperty(Required = Required.DisallowNull)] public NestedData OptionalObject;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string OptionalText;
            public int? OptionalNumber;
        }

        [Test]
        public void AddOptionalFieldCreatesEditableValueInOneStep()
        {
            var members = ConfigurationMetadata.Members(typeof(FieldDefaults)).ToArray();
            var text = members.Single(m => m.PropertyName == "optional-text");
            Assert.AreEqual("", ConfigurationMetadata.MemberDefault(typeof(FieldDefaults), text).Value<string>());
            var obj = members.Single(m => m.PropertyName == "optional-object");
            var created = ConfigurationMetadata.MemberDefault(typeof(FieldDefaults), obj);
            Assert.AreEqual("42", ConfigurationDocument.Serialize(created["renamed"]));
            var number = members.Single(m => m.PropertyName == "optional-number");
            Assert.AreEqual("0", ConfigurationDocument.Serialize(ConfigurationMetadata.MemberDefault(typeof(FieldDefaults), number)));
            Assert.IsFalse(ConfigurationMetadata.IsRequired(obj));
            Assert.IsFalse(ConfigurationMetadata.IsRequired(text));
        }

        [Test]
        public void RequiredFieldsStayPresentButRemovedOptionalFieldsStayAbsent()
        {
            var data = new JObject();
            Assert.IsTrue(ConfigurationMetadata.FillRequired(data, typeof(FieldDefaults)));
            Assert.AreEqual("", data["required-text"].Value<string>());
            Assert.IsNull(data["optional-text"]);
            data["optional-text"] = "edited";
            data.Property("optional-text").Remove();
            Assert.IsFalse(ConfigurationMetadata.FillRequired(data, typeof(FieldDefaults)));
            Assert.IsNull(data["optional-text"]);
            Assert.IsTrue(ConfigurationMetadata.IsRequired(ConfigurationMetadata.Members(typeof(FieldDefaults)).Single(m => m.PropertyName == "required-text")));
        }

        [Test]
        public void TaskKindsAreEnumsAndKeepTheirExistingJsonNames()
        {
            Assert.AreEqual(typeof(TaskConfigKind), ConfigurationMetadata.Members(typeof(TaskConfigEntry)).Single(m => m.PropertyName == "type").PropertyType);
            CollectionAssert.AreEquivalent(Game.Tasks.TaskFactory.TypeNames, Enum.GetNames(typeof(TaskConfigKind)));
            var task = new TaskConfigEntry { Type = TaskConfigKind.WinHands };
            Assert.AreEqual("WinHands", JObject.Parse(ConfigManager.Serialize(task))["type"].Value<string>());
            Assert.AreEqual(TaskConfigKind.WinHands, ConfigManager.Deserialize<TaskConfigEntry>(ConfigManager.Serialize(task)).Type);
        }

        [Test]
        public void RegistryMatchesAllCheckedInFilesAndLoadsTheirTypes()
        {
            var files = ConfigurationDocument.Discover(ConfigManager.RootDirectory);
            foreach (var path in files)
            {
                var type = ConfigurationMetadata.Find(path);
                Assert.IsNotNull(type, path);
                Assert.IsInstanceOf(type, ConfigManager.Load(type));
            }
            Assert.AreEqual(typeof(ItemConfigEntry), ConfigurationMetadata.ElementType(typeof(ItemsConfigData)));
            Assert.IsInstanceOf<JArray>(ConfigurationDocument.Parse(ConfigManager.Serialize(new ItemsConfigData())));
        }

        [Test]
        public void MetadataUsesJsonNamesIgnoresAndPresentationAttributes()
        {
            var members = ConfigurationMetadata.Members(typeof(NestedData)).ToArray();
            Assert.IsFalse(members.Any(p => p.PropertyName == "ignored"));
            var field = members.Single(p => p.PropertyName == "renamed");
            Assert.AreEqual("分组", ConfigurationMetadata.Attribute<HeaderAttribute>(field).header);
            Assert.AreEqual("字段说明", ConfigurationMetadata.Attribute<TooltipAttribute>(field).tooltip);
            Assert.AreEqual(8, ConfigurationMetadata.Attribute<SpaceAttribute>(field).height);
            Assert.AreEqual("42", ConfigurationDocument.Serialize(ConfigurationMetadata.MemberDefault(typeof(NestedData), field)));
        }

        [Test]
        public void NewElementsUseIndependentConstructorsAndNestedDefaults()
        {
            var first = ConfigurationMetadata.NewValue(typeof(LotteryEntryConfigData));
            first["weight"] = 99;
            ((JArray)first["reward"]).Add(new JObject());
            var second = ConfigurationMetadata.NewValue(typeof(LotteryEntryConfigData));
            Assert.AreEqual("1", ConfigurationDocument.Serialize(second["weight"]));
            Assert.IsEmpty((JArray)second["reward"]);
            var reward = ConfigurationMetadata.NewValue(typeof(RewardConfigEntry));
            Assert.AreEqual("Dollars", reward["kind"].Value<string>());
            Assert.AreEqual("1", ConfigurationDocument.Serialize(reward["quantity"]));
            var member = ConfigurationMetadata.Members(typeof(RewardConfigEntry)).Single(p => p.PropertyName == "kind");
            Assert.AreEqual("Dollars", ConfigurationMetadata.MemberDefault(typeof(RewardConfigEntry), member).Value<string>());
        }

        [Test]
        public void LoadingPreservesDefaultsAndReplacesExplicitCollections()
        {
            var defaults = ConfigManager.Deserialize<NestedData>("{}");
            Assert.AreEqual(42, defaults.Value);
            Assert.IsTrue(defaults.Enabled);
            CollectionAssert.AreEqual(new[] { 7, 8 }, defaults.Values);
            var explicitValues = ConfigManager.Deserialize<NestedData>("{\"renamed\":0,\"enabled\":false,\"values\":[1],\"lookup\":{\"a\":[2,3]}}");
            Assert.AreEqual(0, explicitValues.Value);
            Assert.IsFalse(explicitValues.Enabled);
            CollectionAssert.AreEqual(new[] { 1 }, explicitValues.Values);
            CollectionAssert.AreEqual(new[] { 2, 3 }, explicitValues.Lookup["a"]);
        }

        [Test]
        public void LoadingDoesNotRunBusinessValidation()
        {
            var data = ConfigManager.Deserialize<ClientNetworkConfigData>("{\"server-host\":\"localhost\",\"server-port\":0,\"use-ssl\":false,\"connect-timeout-seconds\":10,\"resource-base-url\":\"\"}");
            Assert.AreEqual(0, data.ServerPort);
            Assert.Throws<InvalidDataException>(() => ConfigurationValidation.Validate(data));
        }

        [TestCase("{\"renamed\":\"42\"}")]
        [TestCase("{\"renamed\":1.5}")]
        [TestCase("{\"renamed\":2147483648}")]
        [TestCase("{\"renamed\":1,\"renamed\":2}")]
        [TestCase("{\"typo\":1}")]
        [TestCase("{\"enabled\":1}")]
        public void IncompatibleOrAmbiguousJsonIsRejected(string json)
        {
            Assert.Throws<InvalidDataException>(() => ConfigManager.Deserialize<NestedData>(json));
        }

        [Test]
        public void RequiredFieldsAreNotSilentlyFilledByConstructor()
        {
            var error = Assert.Throws<InvalidDataException>(() => ConfigManager.Deserialize<ShopConfigData>("[{\"item-id\":\"egg\",\"price\":50}]"));
            StringAssert.Contains("listed", error.Message);
            StringAssert.Contains("purchasable", error.Message);
        }

        [Test]
        public void FieldsUseKebabCaseAndPositionalDefaultsHaveTypes()
        {
            var server = JObject.Parse(ConfigManager.Serialize(new ServerNetworkConfigData()));
            Assert.IsNotNull(server["path"]);
            Assert.IsNotNull(server["password"]);
            Assert.IsNull(server["Path"]);
            Assert.IsNotNull(server["listen-port"]);
            var objective = ConfigurationMetadata.NewValue(typeof(TaskObjectiveConfigData));
            Assert.AreEqual("CountDistinctHands", objective["type"].Value<string>());
            Assert.AreEqual("Won", objective["params"][1][0]["type"].Value<string>());
            Assert.AreEqual(typeof(TaskConditionConfigData[]), ConfigurationMetadata.TaskParameterType((JArray)objective["params"], 1));
            var task = ConfigurationMetadata.NewValue(typeof(TaskConfigEntry));
            Assert.IsInstanceOf<JArray>(task["reward"]);
            Assert.IsNull(ConfigurationMetadata.TaskParameterType((JArray)task["params"], 0));
            task["type"] = "FormPattern";
            Assert.AreEqual(typeof(Game.Facts.FormedPattern), ConfigurationMetadata.TaskParameterType((JArray)task["params"], 0));
        }

        [Test]
        public void CreatingExistingConfigurationNeverOverwritesIt()
        {
            var path = ConfigManager.GetPath("Network/client-network.json");
            var before = File.ReadAllBytes(path);
            Assert.Throws<IOException>(() => ConfigManager.CreateFile(typeof(ClientNetworkConfigData)));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }
    }
}
