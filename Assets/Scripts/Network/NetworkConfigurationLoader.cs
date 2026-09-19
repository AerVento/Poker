using Cysharp.Threading.Tasks;
using Framework.Configs;
using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Network
{
    public static class NetworkConfigurationLoader
    {
        public const string ClientFileName = "client-network.json";
        public const string ServerFileName = "server-network.json";
        private const int DownloadTimeoutSeconds = 10;

        public static bool IsServerProcess
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#elif !UNITY_EDITOR && (UNITY_SERVER || UNITY_STANDALONE_LINUX)
                return true;
#else
                return Array.IndexOf(Environment.GetCommandLineArgs(), "-pokerServer") >= 0;
#endif
            }
        }

        public static string GetServerPath()
        {
            var supplied = GetArgument("-pokerServerConfig");
            return supplied == null ? ConfigManager.GetPath("Network", ServerFileName) : Path.GetFullPath(supplied);
        }

        public static string GetClientPath()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new Uri(new Uri(Application.absoluteURL),
                "Configs/Network/" + ClientFileName).AbsoluteUri;
#else
            var supplied = GetArgument("-pokerClientConfig");
            return supplied == null ? ConfigManager.GetPath("Network", ClientFileName) : Path.GetFullPath(supplied);
#endif
        }

        public static ServerNetworkConfiguration LoadAndApplyServer(NetworkRoom manager)
        {
            var path = GetServerPath();
            var configuration = NetworkConfiguration.ParseServer(ConfigManager.ReadAllTextAtPath(path));
            configuration.Apply(manager, path);
            LogInfo($"Server network configuration loaded: port {configuration.ListenPort}, SSL {configuration.UseSsl}, max connections {configuration.MaxConnections}.");
            return configuration;
        }

        public static async UniTask<ClientNetworkConfiguration> LoadAndApplyEditorLocalHostAsync(
            NetworkRoom manager, CancellationToken cancellationToken)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));
            var serverPath = GetServerPath();
            var configuredServer = NetworkConfiguration.ParseServer(ConfigManager.ReadAllTextAtPath(serverPath));
            var configuredClient = await LoadClientAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var localServer = configuredServer.ForEditorLocalHost();
            var localClient = configuredClient.ForEditorLocalHost(localServer.ListenPort);
            localServer.Apply(manager, serverPath);
            manager.ConfigureClient(localClient);
            LogInfo($"Editor local host configuration loaded: {localClient.ServerUri}, SSL false, max connections {localServer.MaxConnections}.");
            return localClient;
        }

        public static async UniTask<ClientNetworkConfiguration> LoadClientAsync(CancellationToken cancellationToken)
        {
            var path = GetClientPath();
            string json;
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                json = await DownloadClientJsonAsync(uri, cancellationToken);
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                json = ConfigManager.ReadAllTextAtPath(path);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var configuration = NetworkConfiguration.ParseClient(json);
#if UNITY_WEBGL && !UNITY_EDITOR
            configuration.ValidatePageUrl(Application.absoluteURL);
#endif
            return configuration;
        }

        public static async UniTask<string> DownloadClientJsonAsync(Uri uri, CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    // Keep this small deployment file independent of cached Unity build data.
                    var url = new UriBuilder(uri) { Query = "_pokerConfig=" + Guid.NewGuid().ToString("N") }.Uri;
                    using var request = UnityWebRequest.Get(url);
                    request.timeout = DownloadTimeoutSeconds;
                    await request.SendWebRequest().WithCancellation(cancellationToken);
                    return request.downloadHandler.text;
                }
                catch (UnityWebRequestException)
                {
                    if (attempt == 1)
                        throw new IOException("Unable to download client network configuration after two attempts.");
                }
                await UniTask.Delay(500, ignoreTimeScale: true, cancellationToken: cancellationToken);
            }
            throw new IOException("Unable to download client network configuration.");
        }

        internal static void LogInfo(string message)
        {
            Framework.Log.Log.WriteInfo(message);
#if !UNITY_EDITOR
            Debug.Log(message);
#endif
        }

        internal static void LogError(string message)
        {
            Framework.Log.Log.WriteError(message);
#if !UNITY_EDITOR
            // Also expose startup failures in Player.log, server stdout and the browser console.
            Debug.LogError(message);
#endif
        }

        private static string GetArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (args[index] != name)
                    continue;
                if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("-"))
                    throw new ArgumentException($"{name} requires a configuration file path.");
                return args[index + 1];
            }
            return null;
        }
    }
}
