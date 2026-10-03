using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.UI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ConfigurationEditorTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/ConfigurationEditorTests/" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_directory, true);
        }

        [Test]
        public void RoundTripPreservesNumbersStringsAndUnknownFields()
        {
            const string json = "{\"large\":18446744073709551616,\"decimal\":0.123456789012345678901234567890,\"exponent\":1e400,\"date\":\"2026-01-01T00:00:00Z\",\"extra\":[null,true,{},[],\"中文\\n引号\\\"\"]}";
            var parsed = ConfigurationDocument.Parse(json);
            Assert.AreEqual(json, ConfigurationDocument.Serialize(parsed));
            Assert.AreEqual(JTokenType.String, parsed["date"].Type);
        }

        [TestCase("{\"a\":1,\"a\":2}")]
        [TestCase("{\"a\": NaN}")]
        [TestCase("{\"a\":01}")]
        [TestCase("{\"a\":")]
        public void InvalidJsonCannotOverwriteFile(string draft)
        {
            var path = Path.Combine(_directory, "Test.json");
            File.WriteAllText(path, "{\"original\":true}");
            var expected = File.ReadAllBytes(path);
            Assert.Catch(() => ConfigurationDocument.Save(path, draft, expected));
            CollectionAssert.AreEqual(expected, File.ReadAllBytes(path));
        }

        [TestCase(true, "\r\n")]
        [TestCase(false, "\n")]
        public void SavePreservesEncodingNewlinesAndValues(bool bom, string newline)
        {
            var path = Path.Combine(_directory, "Test.json");
            File.WriteAllText(path, "{" + newline + "  \"value\": 1" + newline + "}" + newline, new UTF8Encoding(bom));
            ConfigurationDocument.Save(path, "{\"value\":9223372036854775808,\"array\":[{},[],null]}", File.ReadAllBytes(path));
            var result = File.ReadAllBytes(path);
            Assert.AreEqual(bom, result[0] == 0xEF);
            var text = ConfigurationDocument.Decode(result);
            Assert.AreEqual(newline == "\r\n", text.Contains("\r\n"));
            StringAssert.EndsWith(newline, text);
            Assert.AreEqual("9223372036854775808", ConfigurationDocument.Serialize(ConfigurationDocument.Parse(text)["value"]));
            Assert.AreEqual(0, Directory.GetFiles(_directory, "*.tmp").Length);
        }

        [Test]
        public void ExternalChangeAndDeletionAreNotOverwritten()
        {
            var path = Path.Combine(_directory, "Test.json");
            File.WriteAllText(path, "{}");
            var expected = File.ReadAllBytes(path);
            File.WriteAllText(path, "{\"external\":true}");
            Assert.Throws<IOException>(() => ConfigurationDocument.Save(path, "[]", expected));
            Assert.AreEqual("{\"external\":true}", File.ReadAllText(path));
            File.Delete(path);
            Assert.Throws<IOException>(() => ConfigurationDocument.Save(path, "[]", expected));
            Assert.IsFalse(File.Exists(path));
        }

        [Test]
        public void DiscoveryIncludesNewNestedJsonButDoesNotGuessItsAudience()
        {
            Directory.CreateDirectory(Path.Combine(_directory, "NewFeature"));
            File.WriteAllText(Path.Combine(_directory, "NewFeature/Extra.JSON"), "{}");
            File.WriteAllText(Path.Combine(_directory, "ignored.txt"), "{}");
            CollectionAssert.AreEqual(new[] { "NewFeature/Extra.JSON" }, ConfigurationDocument.Discover(_directory));
            Assert.AreEqual(ConfigurationDocument.Audience.Unknown, ConfigurationDocument.Classify("NewFeature/Extra.JSON"));
            Assert.AreEqual(ConfigurationDocument.Audience.Client, ConfigurationDocument.Classify("Network/client-network.json"));
            Assert.AreEqual(ConfigurationDocument.Audience.Server, ConfigurationDocument.Classify("Items/Items.json"));
        }

        [Test]
        public void AllCurrentConfigurationsAreDiscoveredClassifiedAndRoundTrip()
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Configs"));
            var files = ConfigurationDocument.Discover(directory);
            Assert.GreaterOrEqual(files.Length, 13);
            foreach (var file in files)
            {
                Assert.AreNotEqual(ConfigurationDocument.Audience.Unknown, ConfigurationDocument.Classify(file), file);
                var root = ConfigurationDocument.Parse(File.ReadAllText(Path.Combine(directory, file)));
                Assert.IsTrue(JToken.DeepEquals(root, ConfigurationDocument.Parse(ConfigurationDocument.Serialize(root))), file);
            }
        }

        [Test]
        public void UndoAfterSavingKeepsDiskBaselineAndCanBeSavedAgain()
        {
            var path = Path.Combine(_directory, "Test.json");
            File.WriteAllText(path, "{\"value\":1}");
            var window = ScriptableObject.CreateInstance<ConfigurationEditorWindow>();
            try
            {
                Invoke(window, "LoadFile", path);
                Undo.IncrementCurrentGroup();
                Invoke(window, "SetDraft", "{\"value\":2}");
                Undo.FlushUndoRecordObjects();
                Assert.AreEqual(true, Invoke(window, "SaveFile"));
                Undo.PerformUndo();
                Assert.IsTrue(window.hasUnsavedChanges);
                Assert.AreEqual("2", ConfigurationDocument.Serialize(ConfigurationDocument.Parse(File.ReadAllText(path))["value"]));
                Assert.AreEqual(true, Invoke(window, "SaveFile"));
                Assert.AreEqual("1", ConfigurationDocument.Serialize(ConfigurationDocument.Parse(File.ReadAllText(path))["value"]));
            }
            finally
            {
                window.DiscardChanges();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void NetworkValidationChecksBothSidesAndCollectsBothErrors()
        {
            Directory.CreateDirectory(Path.Combine(_directory, "Network"));
            File.WriteAllText(Path.Combine(_directory, "Network/client-network.json"),
                "{\"serverHost\":\"localhost\",\"serverPort\":0,\"useSsl\":false,\"connectTimeoutSeconds\":10}");
            File.WriteAllText(Path.Combine(_directory, "Network/server-network.json"),
                "{\"listenPort\":7777,\"useSsl\":false,\"maxConnections\":0}");
            var errors = new System.Collections.Generic.List<string>();
            ConfigurationDocument.ValidateNetwork(_directory, errors);
            Assert.AreEqual(2, errors.Count);
            Assert.IsTrue(errors.Any(error => error.Contains("client-network.json") && error.Contains("serverPort")));
            Assert.IsTrue(errors.Any(error => error.Contains("server-network.json") && error.Contains("maxConnections")));
        }

        [Test]
        public void NetworkValidationReportsMissingFilesAndCertificate()
        {
            var errors = new System.Collections.Generic.List<string>();
            ConfigurationDocument.ValidateNetwork(_directory, errors);
            Assert.AreEqual(2, errors.Count);
            Directory.CreateDirectory(Path.Combine(_directory, "Network"));
            File.WriteAllText(Path.Combine(_directory, "Network/client-network.json"),
                "{\"server-host\":\"localhost\",\"server-port\":7777,\"use-ssl\":false,\"connect-timeout-seconds\":10,\"resource-base-url\":\"\"}");
            var server = new JObject
            {
                ["listen-port"] = 7777, ["use-ssl"] = true, ["max-connections"] = 100,
                ["path"] = Path.Combine(_directory, "absent.pfx"), ["password"] = ""
            };
            File.WriteAllText(Path.Combine(_directory, "Network/server-network.json"), server.ToString());
            errors.Clear();
            ConfigurationDocument.ValidateNetwork(_directory, errors);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("certificate", errors[0]);
        }

        [Test]
        public void AllConfigurationValidationSucceedsWithoutConsoleMessages()
        {
            var logs = new System.Collections.Generic.List<string>();
            Application.LogCallback capture = (message, stack, type) => logs.Add(message);
            Application.logMessageReceived += capture;
            try
            {
                var errors = ConfigurationDocument.ValidateAll(Path.GetFullPath(Path.Combine(Application.dataPath, "../Configs")));
                Assert.IsEmpty(errors, string.Join("\n", errors));
                Assert.IsEmpty(logs, string.Join("\n", logs));
            }
            finally { Application.logMessageReceived -= capture; }
        }

        [Test]
        public void ArrayMoveAndClonePreserveNestedValues()
        {
            var array = (JArray)ConfigurationDocument.Parse("[{\"id\":\"a\",\"values\":[1,null]},{\"id\":\"b\"}]");
            typeof(ConfigurationEditorWindow).GetMethod("Move", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { array, 0, 1 });
            Assert.AreEqual("a", array[1]["id"].Value<string>());
            array.Insert(2, array[1].DeepClone());
            array[2]["id"] = "c";
            Assert.AreEqual("a", array[1]["id"].Value<string>());
            Assert.AreEqual("[1,null]", ConfigurationDocument.Serialize(array[2]["values"]));
            array.RemoveAt(1);
            Assert.AreEqual(2, array.Count);
        }

        private static object Invoke(ConfigurationEditorWindow window, string method, params object[] args)
        {
            return typeof(ConfigurationEditorWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, args);
        }
    }
}
