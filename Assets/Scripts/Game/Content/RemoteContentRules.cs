using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Content
{
    public enum RemoteContentState { Unavailable, Missing, Stale, Current }

    /// <summary>One bundle of a downloaded content version. Options is the serialized AssetBundleRequestOptions.</summary>
    [Serializable]
    public sealed class RemoteBundleRecord
    {
        public string Url;
        public string Provider;
        public string Options;

        public bool Same(RemoteBundleRecord other) => other != null && Url == other.Url && Options == other.Options;
    }

    /// <summary>The content version a browser keeps for one address, and how to load it without a catalog.</summary>
    [Serializable]
    public sealed class RemoteContentRecord
    {
        public string Address;
        public string AssetId;
        public string AssetProvider;
        public string Format;
        public List<RemoteBundleRecord> Bundles = new List<RemoteBundleRecord>();

        public bool SameContent(RemoteContentRecord other) => other != null && AssetId == other.AssetId &&
            AssetProvider == other.AssetProvider && Bundles.Count == other.Bundles.Count &&
            Bundles.Zip(other.Bundles, (a, b) => a.Same(b)).All(same => same);

        // Bundles of this version that the other version no longer uses.
        public IEnumerable<RemoteBundleRecord> BundlesNotIn(RemoteContentRecord other) =>
            other == null ? Bundles : Bundles.Where(bundle => !other.Bundles.Any(bundle.Same));
    }

    /// <summary>content-version.txt: the UTC build timestamp, the content format and the catalog file name.</summary>
    public sealed class RemoteContentVersion
    {
        public const string FileName = "content-version.txt";
        public string Version { get; }
        public string Format { get; }
        public string Catalog { get; }

        public RemoteContentVersion(string version, string format, string catalog)
        {
            Version = version;
            Format = format;
            Catalog = catalog;
        }

        public static bool TryParse(string text, out RemoteContentVersion version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var values = ParseLines(text);
            if (!values.TryGetValue("version", out var stamp) || !values.TryGetValue("format", out var format) ||
                !values.TryGetValue("catalog", out var catalog) || stamp.Length == 0 || format.Length == 0 ||
                catalog.Length == 0 || catalog.IndexOfAny(new[] { '/', '\\', '?', '#' }) >= 0)
                return false;
            version = new RemoteContentVersion(stamp, format, catalog);
            return true;
        }

        public override string ToString() => "version=" + Version + "\nformat=" + Format + "\ncatalog=" + Catalog + "\n";

        // key=value lines; also used by the page build information.
        public static Dictionary<string, string> ParseLines(string text)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in text.Split('\n'))
            {
                var separator = line.IndexOf('=');
                if (separator > 0) values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
            return values;
        }
    }

    public static class RemoteContentRules
    {
        /// <summary>latest is the address in the accepted remote catalog; null when no catalog is usable.</summary>
        public static RemoteContentState Judge(RemoteContentRecord local, RemoteContentRecord latest, string pageFormat)
        {
            if (latest == null) return RemoteContentState.Unavailable;
            if (!IsUsable(local, pageFormat)) return RemoteContentState.Missing;
            return local.SameContent(latest) ? RemoteContentState.Current : RemoteContentState.Stale;
        }

        // A record written by a page with another content format cannot be read by this page.
        public static bool IsUsable(RemoteContentRecord local, string pageFormat) =>
            local != null && !string.IsNullOrEmpty(pageFormat) && local.Format == pageFormat;

        public static bool Accepts(RemoteContentVersion remote, string pageFormat) =>
            remote != null && !string.IsNullOrEmpty(pageFormat) && remote.Format == pageFormat;

        public static bool ShouldSwitch(RemoteContentVersion current, RemoteContentVersion remote, string pageFormat) =>
            Accepts(remote, pageFormat) && (current == null || current.Version != remote.Version);
    }

    /// <summary>Per-address records persisted in browser storage; at most one usable version per address.</summary>
    public sealed class RemoteContentRecordBook
    {
        [Serializable]
        private sealed class Data { public List<RemoteContentRecord> Records = new List<RemoteContentRecord>(); }

        private readonly Dictionary<string, RemoteContentRecord> _records = new Dictionary<string, RemoteContentRecord>(StringComparer.Ordinal);
        public IEnumerable<RemoteContentRecord> All => _records.Values;

        public static RemoteContentRecordBook Parse(string json)
        {
            var book = new RemoteContentRecordBook();
            if (string.IsNullOrWhiteSpace(json)) return book;
            try
            {
                var data = JsonUtility.FromJson<Data>(json);
                foreach (var record in data?.Records ?? new List<RemoteContentRecord>())
                    if (!string.IsNullOrEmpty(record?.Address) && record.Bundles != null && record.Bundles.Count > 0)
                        book._records[record.Address] = record;
            }
            catch (ArgumentException) { }
            return book;
        }

        public string Serialize() => JsonUtility.ToJson(new Data
        {
            Records = _records.Values.OrderBy(record => record.Address, StringComparer.Ordinal).ToList()
        });

        public RemoteContentRecord Get(string address) =>
            address != null && _records.TryGetValue(address, out var record) ? record : null;

        public void Set(RemoteContentRecord record) => _records[record.Address] = record;
        public bool Remove(string address) => address != null && _records.Remove(address);
    }
}
