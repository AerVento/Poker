using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Content
{
    /// <summary>
    /// Major.minor.patch version of a package that other packages depend on. A requirement is written "major.minor":
    /// the same major version and at least that minor version.
    /// </summary>
    public readonly struct SemanticVersion : IComparable<SemanticVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }

        public SemanticVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public static bool TryParse(string text, out SemanticVersion version) => TryParse(text, 3, out version);

        public static bool TryParseRequirement(string text, out SemanticVersion minimum) => TryParse(text, 2, out minimum);

        private static bool TryParse(string text, int parts, out SemanticVersion version)
        {
            version = default;
            var values = new int[3];
            var split = string.IsNullOrEmpty(text) ? Array.Empty<string>() : text.Split('.');
            if (split.Length != parts) return false;
            for (var i = 0; i < parts; i++)
            {
                var part = split[i];
                if (part.Length == 0 || part.Length > 9) return false;
                foreach (var c in part)
                    if (c < '0' || c > '9') return false;
                values[i] = int.Parse(part);
            }
            version = new SemanticVersion(values[0], values[1], values[2]);
            return true;
        }

        public bool Satisfies(SemanticVersion minimum) => Major == minimum.Major && Minor >= minimum.Minor;

        /// <summary>Whether a version string meets a requirement string; malformed text never does.</summary>
        public static bool Satisfies(string version, string requirement) =>
            TryParse(version, out var parsed) && TryParseRequirement(requirement, out var minimum) && parsed.Satisfies(minimum);

        public int CompareTo(SemanticVersion other)
        {
            if (Major != other.Major) return Major.CompareTo(other.Major);
            return Minor != other.Minor ? Minor.CompareTo(other.Minor) : Patch.CompareTo(other.Patch);
        }

        public override string ToString() => Major + "." + Minor + "." + Patch;
    }

    /// <summary>One declared dependency: another package's address and the minimum "major.minor" version.</summary>
    [Serializable]
    public sealed class RemoteContentDependency
    {
        [SerializeField] private string _address;
        [SerializeField] private string _version;
        public string Address => _address;
        public string Version => _version;

        public RemoteContentDependency() { }

        public RemoteContentDependency(string address, string version)
        {
            _address = address;
            _version = version;
        }
    }

    /// <summary>
    /// A remote root that takes part in package dependencies. Version is empty unless another package depends on it.
    /// Dependencies are logical: the root never references the other package's assets, it receives them once loaded.
    /// </summary>
    public interface IRemoteContentPackage
    {
        string Version { get; }
        IReadOnlyList<RemoteContentDependency> Dependencies { get; }
        /// <summary>The loaded roots of Dependencies, in declaration order. Called before the asset is handed out.</summary>
        void AttachDependencies(IReadOnlyList<UnityEngine.Object> assets);
    }

    /// <summary>content-packages.json: the version and dependencies of every package in one content build.</summary>
    [Serializable]
    public sealed class RemoteContentManifest
    {
        public const string FileName = "content-packages.json";

        [Serializable]
        public sealed class Package
        {
            public string Address;
            public string Version;
            public List<RemoteContentDependency> Dependencies = new List<RemoteContentDependency>();
        }

        public List<Package> Packages = new List<Package>();

        public Package Find(string address)
        {
            foreach (var package in Packages)
                if (package != null && package.Address == address) return package;
            return null;
        }

        public static RemoteContentManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new RemoteContentManifest();
            var manifest = JsonUtility.FromJson<RemoteContentManifest>(json);
            if (manifest?.Packages == null) throw new FormatException("Invalid " + FileName + ".");
            return manifest;
        }
    }
}
