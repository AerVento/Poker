using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Editor
{
    /// <summary>
    /// Stamps client builds with the page version (UTC build time) and the content format, and writes
    /// page-version.txt next to a WebGL page. The stamp is a temporary Resources file removed after the build.
    /// </summary>
    public sealed class ClientBuildStamp : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string PageVersionFileName = "page-version.txt";
        private const string StampPath = "Assets/Resources/" + ClientBuild.ResourceName + ".txt";
        public const string LastPageFormatPath = "Library/PokerBuild/PageContentFormat.txt";
        private static string _pageVersion;
        private static string _format;

        public int callbackOrder => 0;

        private static bool IsClient(BuildReport report) =>
            report.summary.platform == BuildTarget.WebGL ||
            report.summary.platform == BuildTarget.StandaloneWindows64 && !IsServer(report);

        private static bool IsServer(BuildReport report) =>
            EditorUserBuildSettings.standaloneBuildSubtarget == StandaloneBuildSubtarget.Server;

        public void OnPreprocessBuild(BuildReport report)
        {
            DeleteStamp();
            if (!IsClient(report)) return;
            // Pages read only the remote catalog, so a page build never rebuilds content; it only needs the local runtime data.
            if (!File.Exists(Path.Combine(Addressables.BuildPath, "settings.json")))
                throw new BuildFailedException("Build remote content for this platform first: Poker/Remote Artwork/Build for Active Platform.");
            _pageVersion = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            _format = ContentFormatVersion.Compute();
            File.WriteAllText(StampPath, ClientBuild.Format(_pageVersion, _format));
            AssetDatabase.ImportAsset(StampPath, ImportAssetOptions.ForceSynchronousImport);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            DeleteStamp();
            if (report.summary.platform != BuildTarget.WebGL || _pageVersion == null) return;
            File.WriteAllText(Path.Combine(report.summary.outputPath, PageVersionFileName), _pageVersion + "\n");
            Directory.CreateDirectory(Path.GetDirectoryName(LastPageFormatPath));
            File.WriteAllText(LastPageFormatPath, _format);
            Debug.Log("WebGL page " + _pageVersion + ", content format " + _format);
        }

        private static void DeleteStamp()
        {
            if (File.Exists(StampPath)) AssetDatabase.DeleteAsset(StampPath);
        }
    }
}
