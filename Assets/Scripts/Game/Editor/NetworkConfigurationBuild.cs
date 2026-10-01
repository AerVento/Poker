using Game.Network;
using Framework.Configs;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Editor
{
    public sealed class NetworkConfigurationBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
                report.summary.platform != BuildTarget.StandaloneLinux64 &&
                report.summary.platform != BuildTarget.WebGL)
                return;

            var outputRoot = report.summary.platform == BuildTarget.WebGL
                ? report.summary.outputPath
                : Path.GetDirectoryName(report.summary.outputPath);
            var directory = Path.Combine(outputRoot, "Configs", "Network");
            Directory.CreateDirectory(directory);
            // A dedicated server never reads the client file; a Windows build can run as either and needs both.
            // Only standalone builds have a subtarget; reading it on WebGL throws.
            var dedicatedServer = report.summary.platformGroup == BuildTargetGroup.Standalone &&
                report.summary.GetSubtarget<StandaloneBuildSubtarget>() == StandaloneBuildSubtarget.Server;
            if (!dedicatedServer)
                CopyIfMissing(NetworkConfigurationLoader.ClientFileName, directory);
            if (report.summary.platform != BuildTarget.WebGL)
                CopyIfMissing(NetworkConfigurationLoader.ServerFileName, directory);
        }

        private static void CopyIfMissing(string fileName, string directory)
        {
            var destination = Path.Combine(directory, fileName);
            if (!File.Exists(destination))
                ConfigManager.CopyFileAtPath(ConfigManager.GetPath("Network", fileName), destination, false);
        }
    }
}
