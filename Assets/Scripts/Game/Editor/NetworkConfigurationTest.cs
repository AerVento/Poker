using Game.Network;
using Mirror;
using Mirror.SimpleWeb;
using NUnit.Framework;
using System;
using System.IO;
using System.Security.Authentication;
using UnityEngine;

namespace Game.Tests
{
    public sealed class NetworkConfigurationTest
    {
        private const string ClientJson = "{\"serverHost\":\"127.0.0.1\",\"serverPort\":17891,\"useSsl\":false,\"connectTimeoutSeconds\":3}";
        private const string ServerJson = "{\"listenPort\":17891,\"useSsl\":false,\"path\":\"\",\"password\":\"\",\"maxConnections\":12}";

        [Test]
        public void Client_ParsesExplicitEndpointAndTimeout()
        {
            var configuration = NetworkConfiguration.ParseClient(ClientJson);
            Assert.AreEqual("ws://127.0.0.1:17891/", configuration.ServerUri.AbsoluteUri);
            Assert.AreEqual(3, configuration.ConnectTimeoutSeconds);
        }

        [Test]
        public void Client_ParsesEncryptedDomainAndIPv6()
        {
            var json = ClientJson.Replace("127.0.0.1", "poker.example.com").Replace("false", "true").Replace("17891", "443");
            Assert.AreEqual("wss://poker.example.com/", NetworkConfiguration.ParseClient(json).ServerUri.AbsoluteUri);
            Assert.AreEqual("ws://[::1]:17891/", NetworkConfiguration.ParseClient(ClientJson.Replace("127.0.0.1", "::1")).ServerUri.AbsoluteUri);
        }

        [TestCase("https://poker.example.com")]
        [TestCase("127.0.0.1:7777")]
        [TestCase("poker.example.com/path")]
        [TestCase("user@poker.example.com")]
        [TestCase(" localhost")]
        [TestCase("")]
        public void Client_RejectsHostContainingEndpointParts(string host)
        {
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseClient(ClientJson.Replace("127.0.0.1", host)));
        }

        [TestCase("0")]
        [TestCase("65536")]
        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase("\"7777\"")]
        public void BothSides_RejectInvalidPorts(string port)
        {
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseClient(ClientJson.Replace("17891", port)));
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseServer(ServerJson.Replace("17891", port)));
        }

        [TestCase("0")]
        [TestCase("301")]
        [TestCase("null")]
        public void Client_RejectsInvalidTimeout(string value)
        {
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseClient(ClientJson.Replace("Seconds\":3", "Seconds\":" + value)));
        }

        [Test]
        public void Client_RequiresExplicitBooleanSslAndAllConnectionFields()
        {
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseClient(ClientJson.Replace("false", "\"false\"")));
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseClient(ClientJson.Replace("\"useSsl\":false,", "")));
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseClient(ClientJson.Replace("\"serverPort\":17891,", "")));
        }

        [Test]
        public void BothSides_RejectDuplicateUnknownAndMalformedConfiguration()
        {
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseClient(ClientJson.Replace("}", ",\"useSsl\":true}")));
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseServer(ServerJson.Replace("}", ",\"unknown\":true}")));
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseClient("[]"));
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseServer("null"));
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseClient(""));
        }

        [Test]
        public void ParsingFailure_DoesNotEchoServerSecrets()
        {
            var error = Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseServer("{\"password\": secret-value}"));
            StringAssert.DoesNotContain("secret-value", error.Message);
            Assert.IsNull(error.InnerException);
        }

        [Test]
        public void Client_HttpsPageRejectsPlainWebsocketWithoutDowngrading()
        {
            var plain = NetworkConfiguration.ParseClient(ClientJson);
            Assert.Throws<InvalidDataException>(() => plain.ValidatePageUrl("https://example.com/game/"));
            Assert.DoesNotThrow(() => plain.ValidatePageUrl("http://localhost/game/"));
            Assert.DoesNotThrow(() => NetworkConfiguration.ParseClient(ClientJson.Replace("false", "true"))
                .ValidatePageUrl("https://example.com/game/"));
        }

        [Test]
        public void Server_PlainModeDoesNotRequireOrLoadCertificate()
        {
            var configuration = NetworkConfiguration.ParseServer("{\"listenPort\":7777,\"useSsl\":false,\"maxConnections\":100}");
            Assert.DoesNotThrow(configuration.ValidateCertificate);
            Assert.AreEqual(string.Empty, configuration.CertificatePassword);
        }

        [Test]
        public void Server_SslRequiresAbsoluteCertificatePathAndExistingFile()
        {
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseServer(ServerJson.Replace("false", "true")));
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseServer(ServerJson.Replace("false", "true").Replace("\"path\":\"\"", "\"path\":\"relative.pfx\"")));
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pfx");
            var json = ServerJson.Replace("false", "true").Replace("\"path\":\"\"", "\"path\":" + System.Text.Json.JsonSerializer.Serialize(path));
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseServer(json).ValidateCertificate());
        }

        [Test]
        public void Server_RejectsInvalidConnectionLimit()
        {
            Assert.Throws<InvalidDataException>(() => NetworkConfiguration.ParseServer(ServerJson.Replace("Connections\":12", "Connections\":0")));
        }

        [Test]
        public void ServerJson_IsAlsoTheMirrorCertificateConfiguration()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            var certificate = Path.Combine(Path.GetTempPath(), "server.pfx");
            try
            {
                var json = ServerJson.Replace("false", "true")
                    .Replace("\"path\":\"\"", "\"path\":" + System.Text.Json.JsonSerializer.Serialize(certificate))
                    .Replace("\"password\":\"\"", "\"password\":\"p@ss\\\"word\"");
                File.WriteAllText(path, json);
                var parsed = NetworkConfiguration.ParseServer(json);
                var mirror = SslConfigLoader.Load(true, path, SslProtocols.Tls12);
                Assert.AreEqual(parsed.CertificatePath, mirror.certPath);
                Assert.AreEqual(parsed.CertificatePassword, mirror.certPassword);
                Assert.AreEqual("p@ss\"word", mirror.certPassword);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Apply_OverridesPrefabSslPortAndCapacityWithoutCouplingClientPortToListener()
        {
            var root = new GameObject("Network configuration test");
            root.SetActive(false);
            try
            {
                var manager = root.AddComponent<NetworkRoom>();
                var transport = root.AddComponent<SimpleWebTransport>();
                manager.transport = transport;
                transport.sslEnabled = true;
                transport.clientUseWss = true;
                transport.port = 9999;
                manager.networkAddress = "old.example.com";
                var client = NetworkConfiguration.ParseClient(ClientJson);
                manager.ConfigureClient(client);
                Assert.AreEqual("127.0.0.1", manager.networkAddress);
                Assert.IsFalse(transport.sslEnabled);
                Assert.IsFalse(transport.clientUseWss);
                Assert.AreEqual(WebsocketPortOption.SpecifyPort, transport.clientWebsocketSettings.ClientPortOption);
                Assert.AreEqual(17891, transport.clientWebsocketSettings.CustomClientPort);
                Assert.AreEqual(9999, transport.port);
                var file = Path.Combine(Path.GetTempPath(), "server-network.json");
                NetworkConfiguration.ParseServer(ServerJson).Apply(manager, file);
                Assert.AreEqual(17891, transport.port);
                Assert.AreEqual(12, manager.maxConnections);
                Assert.AreEqual(file, transport.sslCertJson);
                Assert.AreEqual(SslProtocols.Tls12, transport.sslProtocols);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
