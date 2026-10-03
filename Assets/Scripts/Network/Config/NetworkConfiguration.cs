using Mirror;
using Mirror.SimpleWeb;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Game.Network
{
    public sealed class ClientNetworkConfiguration
    {
        public string ServerHost { get; }
        public ushort ServerPort { get; }
        public bool UseSsl { get; }
        public int ConnectTimeoutSeconds { get; }
        public string ResourceBaseUrl { get; }
        public Uri ServerUri => new UriBuilder(UseSsl ? "wss" : "ws", ServerHost, ServerPort).Uri;

        internal ClientNetworkConfiguration(string host, ushort port, bool useSsl, int timeout, string resourceBaseUrl = "")
        {
            ServerHost = host;
            ServerPort = port;
            UseSsl = useSsl;
            ConnectTimeoutSeconds = timeout;
            ResourceBaseUrl = resourceBaseUrl;
        }

        public ClientNetworkConfiguration ForEditorLocalHost(ushort serverPort)
            => new ClientNetworkConfiguration("127.0.0.1", serverPort, false, ConnectTimeoutSeconds, ResourceBaseUrl);

        public void ValidatePageUrl(string pageUrl)
        {
            if (!UseSsl && Uri.TryCreate(pageUrl, UriKind.Absolute, out var page) && page.Scheme == "https")
                throw new InvalidDataException("An HTTPS game page requires client useSsl to be true.");
        }

        public void Apply(NetworkManager manager)
        {
            var transport = NetworkConfiguration.GetTransport(manager);
            // Server-side SSL in a prefab must not force a client configured for plain WebSockets to use WSS.
            transport.sslEnabled = false;
            transport.clientUseWss = UseSsl;
            transport.clientWebsocketSettings = new ClientWebsocketSettings
            {
                ClientPortOption = WebsocketPortOption.SpecifyPort,
                CustomClientPort = ServerPort,
            };
            manager.networkAddress = ServerHost;
        }
    }

    public sealed class ServerNetworkConfiguration
    {
        public ushort ListenPort { get; }
        public bool UseSsl { get; }
        public string CertificatePath { get; }
        public string CertificatePassword { get; }
        public int MaxConnections { get; }

        internal ServerNetworkConfiguration(ushort port, bool useSsl, string path, string password, int maxConnections)
        {
            ListenPort = port;
            UseSsl = useSsl;
            CertificatePath = path;
            CertificatePassword = password;
            MaxConnections = maxConnections;
        }

        public ServerNetworkConfiguration ForEditorLocalHost()
            => new ServerNetworkConfiguration(ListenPort, false, string.Empty, string.Empty, MaxConnections);

        public void ValidateCertificate()
        {
            if (!UseSsl)
                return;

            if (!File.Exists(CertificatePath))
                throw new InvalidDataException("The configured SSL certificate file does not exist.");
            try
            {
                using var certificate = new X509Certificate2(CertificatePath, CertificatePassword);
                if (!certificate.HasPrivateKey)
                    throw new InvalidDataException("The SSL certificate must include its private key.");
                var now = DateTime.UtcNow;
                if (now < certificate.NotBefore.ToUniversalTime() || now >= certificate.NotAfter.ToUniversalTime())
                    throw new InvalidDataException("The SSL certificate is expired or not yet valid.");
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                throw new InvalidDataException("Unable to load the SSL certificate; check its format and password.");
            }
        }

        public void Apply(NetworkManager manager, string configurationPath)
        {
            var transport = NetworkConfiguration.GetTransport(manager);
            ValidateCertificate();
            transport.port = ListenPort;
            transport.sslEnabled = UseSsl;
            transport.clientUseWss = false;
            transport.sslProtocols = SslProtocols.Tls12;
            // Mirror reads the top-level path/password fields from this same JSON file.
            transport.sslCertJson = Path.GetFullPath(configurationPath);
            manager.maxConnections = MaxConnections;
        }
    }

    public static class NetworkConfiguration
    {
        public static ClientNetworkConfiguration ParseClient(string json) =>
            ConfigurationValidation.Build(Framework.Configs.ConfigManager.Deserialize<ClientNetworkConfigData>(json));

        public static ServerNetworkConfiguration ParseServer(string json) =>
            ConfigurationValidation.Build(Framework.Configs.ConfigManager.Deserialize<ServerNetworkConfigData>(json));

        internal static SimpleWebTransport GetTransport(NetworkManager manager)
        {
            if (manager == null || !(manager.transport is SimpleWebTransport transport))
                throw new InvalidOperationException("Network configuration requires a SimpleWebTransport on NetworkRoom.");
            if (NetworkServer.active || NetworkClient.active)
                throw new InvalidOperationException("Stop networking before applying network configuration.");
            return transport;
        }

    }
}
