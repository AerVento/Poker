using System.IO;
using Framework.Configs;
using Game.Network;
using Game.Network.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Editor
{
    public sealed class ServerConfigurationValidationBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            try { Validate(); }
            catch (System.Exception error) { throw new BuildFailedException(error.Message); }
        }

        // The same validation the server runs at startup: items, shop, starter grants, daily tasks and the slot lottery.
        [MenuItem("Poker/Validate Server Configuration")]
        public static void Validate()
        {
            ServerConfiguration.Load();
            UnityEngine.Debug.Log("Server configuration is valid.");
        }
    }

    public sealed class CatalogConfigurationBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
                report.summary.platform != BuildTarget.StandaloneLinux64)
                return;

            var root = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), ConfigManager.CONFIG_SAVE_PATH);
            foreach (var (section, file) in CatalogConfiguration.Files)
                Copy(ConfigManager.GetPath(section, file), Path.Combine(root, section, file));
            Copy(ServerConfiguration.StarterGrantsPath,
                Path.Combine(root, ServerConfiguration.AccountsSection, ServerConfiguration.StarterGrantsFile));
            foreach (var file in ServerTaskConfigurationLoader.FileNames)
                Copy(Path.Combine(ServerTaskConfigurationLoader.DefaultDirectory, file), Path.Combine(root, "DailyTasks", file));
        }

        private static void Copy(string source, string target)
        {
            if (!File.Exists(source) || File.Exists(target)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            ConfigManager.CopyFileAtPath(source, target, false);
        }
    }
}
