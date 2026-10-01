#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using Game.Network;
using Mirror;
using UnityEngine;

namespace Game
{
    public partial class GameController
    {
        private async UniTask InitializeEditorLocalHostAsync()
        {
            HasNetworkInitializationStarted = true;
            IsNetworkInitializing = true;
            IsClientNetworkReady = false;
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            try
            {
                if (!(NetworkManager.singleton is NetworkRoom networkRoom))
                    throw new System.InvalidOperationException("NetworkRoom is missing from the startup scene.");
                if (NetworkServer.active || NetworkClient.active)
                    throw new System.InvalidOperationException("Networking was already active before Editor host startup.");

                var configuration = await NetworkConfigurationLoader.LoadAndApplyEditorLocalHostAsync(
                    networkRoom, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                IsClientNetworkReady = true;
                _ = ShopConfiguration.Server;
                networkRoom.StartHost();
                if (!NetworkServer.active || !NetworkClient.active || !networkRoom.transport.ServerActive())
                    throw new System.InvalidOperationException("The Editor local host did not start completely.");
                Debug.Log($"Poker Editor host is listening at {configuration.ServerUri} (SSL disabled).");
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (System.Exception exception)
            {
                IsClientNetworkReady = false;
                if (NetworkManager.singleton is NetworkRoom networkRoom)
                {
                    if (NetworkServer.active && NetworkClient.active)
                        networkRoom.StopHost();
                    else if (NetworkClient.active)
                        networkRoom.StopClient();
                    else if (NetworkServer.active)
                        networkRoom.StopServer();
                }
                Debug.LogError("Editor local host startup failed: " + exception.Message);
            }
            finally
            {
                IsNetworkInitializing = false;
            }
        }
    }
}
#endif
