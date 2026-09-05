using Game.Network;
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
                report.summary.platform != BuildTarget.StandaloneLinux64)
                return;

            var directory = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "Configs");
            var destination = Path.Combine(directory, NetworkConfigurationLoader.ServerFileName);
            if (File.Exists(destination))
                return;
            Directory.CreateDirectory(directory);
            File.Copy(Path.Combine("Configs", NetworkConfigurationLoader.ServerFileName), destination);
        }
    }
}
