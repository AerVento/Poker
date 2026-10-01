using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.UI.Gifts.Editor
{
    /// <summary>IL2CPP players call the gift host through xLua's generated wrapper; a stale wrapper misses methods.</summary>
    public sealed class GiftScriptBuildCheck : IPreprocessBuildWithReport
    {
        private const string WrapperPath = "Assets/XLua/Gen/Game_UI_Gifts_GiftScriptHostWrap.cs";
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL && report.summary.platform != BuildTarget.StandaloneWindows64) return;
            if (!File.Exists(WrapperPath)) throw new BuildFailedException("Missing xLua generated code. Run XLua/Generate Code.");
            var wrapper = File.ReadAllText(WrapperPath);
            var missing = typeof(GiftScriptHost).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && !wrapper.Contains("\"" + method.Name + "\"")).Select(method => method.Name).ToArray();
            if (missing.Length > 0)
                throw new BuildFailedException("xLua generated code is stale (" + string.Join(", ", missing) + "). Run XLua/Generate Code.");
        }
    }
}
