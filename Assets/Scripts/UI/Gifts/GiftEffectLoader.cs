using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Content;
using Game.SO;
using UnityEngine;

namespace Game.UI.Gifts
{
    internal static class GiftEffectLoader
    {
        public static async UniTask<RemoteAssets.Lease<GiftEffectSO>> LoadAsync(
            string address, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var lease = await RemoteAssets.AcquireAsync<GiftEffectSO>(address, cancellationToken);
                // Ready means downloaded, loaded and, for scripted gifts, the Lua modules validated.
                if (lease != null && lease.Asset.IsPlayable) return lease;
                lease?.Dispose();
                if (attempt < 2)
                    await UniTask.Delay(TimeSpan.FromSeconds(30), ignoreTimeScale: true,
                        cancellationToken: cancellationToken);
            }
            Debug.LogWarning("Gift effect unavailable after retries: " + address);
            return null;
        }
    }
}
