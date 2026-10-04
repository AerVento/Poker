using System;
using System.Collections.Generic;
using Game.Content;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.SO
{
    /// <summary>
    /// Root of a remote Lua package: shared modules that other packages declare as a dependency and require by name.
    /// Prepare Catalogs and Groups collects the modules from the package folder; only the version is edited by hand
    /// (Docs/Standards/LuaConvention.md).
    /// </summary>
    public sealed class LuaLibrarySO : ScriptableObject, IRemoteContentPackage
    {
        [Tooltip("major.minor.patch; raise it whenever a module changes (Docs/Standards/LuaConvention.md).")]
        [SerializeField] private string _version = "1.0.0";
        [Tooltip("Collected from the package folder by Poker/Remote Artwork/Prepare Catalogs and Groups.")]
        [SerializeField] private TextAsset[] _modules = Array.Empty<TextAsset>();

        public string Version => _version;
        public IReadOnlyList<RemoteContentDependency> Dependencies => Array.Empty<RemoteContentDependency>();
        public IReadOnlyList<TextAsset> Modules => _modules;

        public void AttachDependencies(IReadOnlyList<Object> assets) { }
    }
}
