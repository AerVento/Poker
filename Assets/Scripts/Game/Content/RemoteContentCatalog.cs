using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Game.Content
{
    /// <summary>
    /// The accepted remote catalog. It is only read to produce records; it is never registered with Addressables,
    /// so the catalog built into the page cannot answer an address.
    /// </summary>
    public sealed class RemoteContentCatalog
    {
        private readonly IResourceLocator _locator;
        private readonly RemoteContentManifest _manifest;
        private readonly Dictionary<string, RemoteContentRecord> _records = new Dictionary<string, RemoteContentRecord>(StringComparer.Ordinal);
        public RemoteContentVersion Version { get; }

        /// <param name="manifestJson">content-packages.json of the same build; null when the build has none.</param>
        public RemoteContentCatalog(RemoteContentVersion version, string json, string manifestJson = null)
        {
            Version = version;
            var data = JsonUtility.FromJson<ContentCatalogData>(json);
            if (data == null) throw new ArgumentException("Remote catalog is empty.");
            _locator = data.CreateLocator();
            _manifest = RemoteContentManifest.Parse(manifestJson);
        }

        public RemoteContentRecord Find(string address)
        {
            if (string.IsNullOrEmpty(address)) return null;
            if (_records.TryGetValue(address, out var cached)) return cached;
            RemoteContentRecord record = null;
            if (_locator.Locate(address, typeof(UnityEngine.Object), out var locations))
            {
                var asset = locations.FirstOrDefault(location => location.HasDependencies);
                if (asset != null) record = ToRecord(address, asset, Version.Format, _manifest.Find(address));
            }
            _records[address] = record;
            return record;
        }

        public static RemoteContentRecord ToRecord(string address, IResourceLocation asset, string format,
            RemoteContentManifest.Package package = null) => new RemoteContentRecord
        {
            Address = address,
            AssetId = asset.InternalId,
            AssetProvider = asset.ProviderId,
            Format = format,
            Bundles = asset.Dependencies.Select(bundle => new RemoteBundleRecord
            {
                Url = bundle.InternalId,
                Provider = bundle.ProviderId,
                Options = bundle.Data is AssetBundleRequestOptions options ? JsonUtility.ToJson(options) : string.Empty
            }).ToList(),
            Version = package?.Version ?? string.Empty,
            Dependencies = package?.Dependencies?.Where(d => d != null).ToList() ?? new List<RemoteContentDependency>()
        };

        /// <summary>Rebuilds Addressables locations from a record, so an old version loads without its catalog.</summary>
        public static IResourceLocation ToLocation(RemoteContentRecord record, Type type)
        {
            var bundles = record.Bundles.Select(bundle => (IResourceLocation)new ResourceLocationBase(
                bundle.Url, bundle.Url, bundle.Provider, typeof(IAssetBundleResource))
            {
                Data = Options(bundle)
            }).ToArray();
            return new ResourceLocationBase(record.Address, record.AssetId, record.AssetProvider, type, bundles);
        }

        public static AssetBundleRequestOptions Options(RemoteBundleRecord bundle) =>
            string.IsNullOrEmpty(bundle.Options) ? null : JsonUtility.FromJson<AssetBundleRequestOptions>(bundle.Options);
    }
}
