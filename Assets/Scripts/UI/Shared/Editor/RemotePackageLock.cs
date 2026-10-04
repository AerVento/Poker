using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Content;
using UnityEditor;
using UnityEngine;

namespace Game.UI.Editor
{
    /// <summary>
    /// packages-lock.json: each versioned package as of the last content build, so a build can tell when content
    /// changed without its version (Docs/Standards/LuaConvention.md). Committed with the content it describes.
    /// </summary>
    [Serializable]
    public sealed class RemotePackageLock
    {
        public const string FilePath = "Assets/RemoteContent/packages-lock.json";

        [Serializable]
        public sealed class Package
        {
            public string Address;
            public string Version;
            // Hash of the root's source files, without the version field itself; platform independent.
            public string Hash;
            // Lua packages: every module ("GiftMotion") and every name its table offers ("GiftMotion.arc").
            public List<string> Exports = new List<string>();
        }

        public List<Package> Packages = new List<Package>();

        public Package Find(string address) => Packages.FirstOrDefault(package => package.Address == address);

        public static RemotePackageLock Load() => File.Exists(FilePath)
            ? JsonUtility.FromJson<RemotePackageLock>(File.ReadAllText(FilePath)) ?? new RemotePackageLock()
            : new RemotePackageLock();

        public void Save()
        {
            Packages = Packages.OrderBy(package => package.Address, StringComparer.Ordinal).ToList();
            File.WriteAllText(FilePath, JsonUtility.ToJson(this, true).Replace("\r\n", "\n") + "\n");
            AssetDatabase.ImportAsset(FilePath);
        }

        /// <summary>
        /// Null when current may follow locked; otherwise what is wrong and the lowest acceptable version. Removing a
        /// module or name needs a new major version, adding one a new minor version, any other change a new patch.
        /// </summary>
        public static string Compare(Package locked, Package current)
        {
            if (!SemanticVersion.TryParse(current.Version, out var now))
                return current.Address + " has an invalid version '" + current.Version + "'; use major.minor.patch.";
            if (locked == null || !SemanticVersion.TryParse(locked.Version, out var before)) return null;
            if (now.CompareTo(before) < 0) return current.Address + " version " + now + " is lower than the locked " + before + ".";
            var lockedExports = locked.Exports ?? new List<string>();
            var currentExports = current.Exports ?? new List<string>();
            var removed = lockedExports.Except(currentExports, StringComparer.Ordinal).ToArray();
            var added = currentExports.Except(lockedExports, StringComparer.Ordinal).ToArray();
            SemanticVersion required;
            string reason;
            if (removed.Length > 0)
            {
                required = new SemanticVersion(before.Major + 1, 0, 0);
                reason = "removed " + string.Join(", ", removed);
            }
            else if (added.Length > 0)
            {
                required = new SemanticVersion(before.Major, before.Minor + 1, 0);
                reason = "added " + string.Join(", ", added);
            }
            else if (current.Hash != locked.Hash)
            {
                required = new SemanticVersion(before.Major, before.Minor, before.Patch + 1);
                reason = "changed content";
            }
            else return null;
            return now.CompareTo(required) >= 0 ? null
                : current.Address + " " + reason + "; raise its version to at least " + required + " (locked " + before + ").";
        }
    }
}
