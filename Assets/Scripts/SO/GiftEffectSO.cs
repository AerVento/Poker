using System;
using System.Collections.Generic;
using System.Linq;
using Game.UI.Gifts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.SO
{
    /// <summary>
    /// Root of one remote gift bundle: the gift's Lua entry module, the shared modules it requires, and the
    /// resources its script may use by name. Every script and asset travels in the same bundle.
    /// </summary>
    [CreateAssetMenu(fileName = "GiftEffect", menuName = "Poker/Gifts/Remote Effect")]
    public sealed class GiftEffectSO : ScriptableObject
    {
        [Serializable]
        public sealed class Resource
        {
            [SerializeField] private string _name;
            [SerializeField] private Object _asset;
            public string Name => _name;
            public Object Asset => _asset;
        }

        [Tooltip("Entry Lua module (Assets/Scripts/UI/Gifts/Lua/<Gift>.lua.txt).")]
        [SerializeField] private TextAsset _script;
        [Tooltip("Shared modules required by the entry module (Lua/Shared/*.lua.txt).")]
        [SerializeField] private TextAsset[] _modules = Array.Empty<TextAsset>();
        [Tooltip("Sprites, audio clips and UI prefabs the script uses by name.")]
        [SerializeField] private Resource[] _resources = Array.Empty<Resource>();

        [NonSerialized] private GiftScript _definition;
        [NonSerialized] private string _error;
#if UNITY_EDITOR
        [NonSerialized] private bool _readSourcesFromDisk;
#endif

        public bool HasScript => _script != null;
        public TextAsset Script => _script;
        public IReadOnlyList<TextAsset> Modules => _modules;
        public IReadOnlyList<Resource> Resources => _resources;
        public string ScriptError => _error;

        public static string ModuleName(TextAsset asset) =>
            asset.name.EndsWith(".lua", StringComparison.Ordinal) ? asset.name.Substring(0, asset.name.Length - 4) : asset.name;

        /// <summary>Ready to play: the script loads, validates and finds every resource it declares.</summary>
        public bool IsPlayable => TryDefine(out _);
        public float Duration => TryDefine(out var script) ? script.Duration : 0f;
        public bool SupportsTimeline => HasScript;

        public bool TryDefine(out GiftScript script)
        {
            script = _definition;
            if (script != null) return true;
            if (!HasScript || _error != null) return false;
            try
            {
                var sources = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var module in _modules.Where(m => m != null).Prepend(_script))
                    sources[ModuleName(module)] = ReadSource(module);
                script = _definition = GiftScriptRuntime.Define(ModuleName(_script), sources);
                foreach (var name in script.Resources)
                    if (FindResource(name) == null) throw new GiftScriptException("Missing gift resource: " + name);
                return true;
            }
            catch (Exception exception)
            {
                // Repeated plays of a broken module fail quietly after the first report.
                _definition = null;
                script = null;
                ReportError(exception.Message);
                return false;
            }
        }

        public void ReportError(string message)
        {
            if (_error != null) return;
            _error = message;
            Debug.LogWarning("Gift script " + name + " unavailable: " + message);
        }

        public Object FindResource(string resourceName)
        {
            foreach (var resource in _resources)
                if (resource != null && resource.Name == resourceName) return resource.Asset;
            return null;
        }

        public GiftEffect CreateEffect(Transform parent) =>
            TryDefine(out var script) ? LuaGiftEffect.Create(this, script, parent) : null;

#if UNITY_EDITOR
        /// <summary>Editor previews read the .lua.txt files on disk so edits apply on the next play.</summary>
        public void ReloadFromDisk()
        {
            _readSourcesFromDisk = true;
            _definition = null;
            _error = null;
        }
#endif

        private string ReadSource(TextAsset module)
        {
#if UNITY_EDITOR
            if (_readSourcesFromDisk)
            {
                var path = UnityEditor.AssetDatabase.GetAssetPath(module);
                if (!string.IsNullOrEmpty(path)) return System.IO.File.ReadAllText(path);
            }
#endif
            return module.text;
        }
    }
}
