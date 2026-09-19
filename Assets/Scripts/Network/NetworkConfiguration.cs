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
        public Uri ServerUri => new UriBuilder(UseSsl ? "wss" : "ws", ServerHost, ServerPort).Uri;

        internal ClientNetworkConfiguration(string host, ushort port, bool useSsl, int timeout)
        {
            ServerHost = host;
            ServerPort = port;
            UseSsl = useSsl;
            ConnectTimeoutSeconds = timeout;
        }

        public ClientNetworkConfiguration ForEditorLocalHost(ushort serverPort)
            => new ClientNetworkConfiguration("127.0.0.1", serverPort, false, ConnectTimeoutSeconds);

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
        public static ClientNetworkConfiguration ParseClient(string json)
        {
            using var document = Parse(json);
            var root = document.RootElement;
            ValidateFields(root, "serverHost", "serverPort", "useSsl", "connectTimeoutSeconds");
            var host = ReadString(root, "serverHost");
            if (string.IsNullOrWhiteSpace(host) || host.Trim() != host ||
                host.IndexOfAny(new[] { '/', '\\', '@', '?', '#', ' ', '\t', '\r', '\n' }) >= 0 ||
                Uri.CheckHostName(host) == UriHostNameType.Unknown)
            {
                throw new InvalidDataException("serverHost must be a host name or IP address without a scheme, port or path.");
            }

            return new ClientNetworkConfiguration(host,
                (ushort)ReadInteger(root, "serverPort", 1, ushort.MaxValue),
                ReadBoolean(root, "useSsl"), ReadInteger(root, "connectTimeoutSeconds", 1, 300));
        }

        public static ServerNetworkConfiguration ParseServer(string json)
        {
            using var document = Parse(json);
            var root = document.RootElement;
            ValidateFields(root, "listenPort", "useSsl", "path", "password", "maxConnections");
            var useSsl = ReadBoolean(root, "useSsl");
            var path = ReadString(root, "path", optional: !useSsl);
            var password = ReadString(root, "password", optional: true);
            if (useSsl && (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)))
                throw new InvalidDataException("SSL path must be an absolute certificate file path.");

            return new ServerNetworkConfiguration(
                (ushort)ReadInteger(root, "listenPort", 1, ushort.MaxValue), useSsl, path, password,
                ReadInteger(root, "maxConnections", 1, int.MaxValue));
        }

        internal static SimpleWebTransport GetTransport(NetworkManager manager)
        {
            if (manager == null || !(manager.transport is SimpleWebTransport transport))
                throw new InvalidOperationException("Network configuration requires a SimpleWebTransport on NetworkRoom.");
            if (NetworkServer.active || NetworkClient.active)
                throw new InvalidOperationException("Stop networking before applying network configuration.");
            return transport;
        }

        private static JsonDocument Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("Network configuration is empty.");
            try
            {
                return JsonDocument.Parse(json.TrimStart('\uFEFF'));
            }
            catch (JsonException)
            {
                // Do not put the JSON or a token from it into logs: server JSON can contain a password.
                throw new InvalidDataException("Network configuration is not valid JSON.");
            }
        }

        private static void ValidateFields(JsonElement root, params string[] allowed)
        {
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Network configuration must be a JSON object.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (Array.IndexOf(allowed, property.Name) < 0 || !seen.Add(property.Name))
                    throw new InvalidDataException("Network configuration contains an unknown or duplicate field.");
            }
        }

        private static string ReadString(JsonElement root, string name, bool optional = false)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                if (optional)
                    return string.Empty;
                throw new InvalidDataException($"Missing required network field: {name}.");
            }
            if (value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"Network field {name} must be a string.");
            return value.GetString();
        }

        private static bool ReadBoolean(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var value) ||
                (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
                throw new InvalidDataException($"Network field {name} must be true or false.");
            return value.GetBoolean();
        }

        private static int ReadInteger(JsonElement root, string name, int minimum, int maximum)
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number ||
                !value.TryGetInt32(out var number) || number < minimum || number > maximum)
                throw new InvalidDataException($"Network field {name} must be an integer from {minimum} to {maximum}.");
            return number;
        }
    }
}
