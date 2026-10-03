using System.IO;
using Framework.Configs;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Network
{
    public sealed class SlotLotteryConfigurationBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -90;
        public void OnPreprocessBuild(BuildReport report) => Validate();

        [MenuItem("Poker/Slot Machine/Validate Configuration")]
        public static void Validate()
        {
            SlotLotteryConfiguration.Load(CatalogConfiguration.Load().MaxQuantityOf);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
                report.summary.platform != BuildTarget.StandaloneLinux64) return;
            var target = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "Configs", "SlotMachine", "Lottery.json");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (!File.Exists(target)) ConfigManager.CopyFileAtPath(SlotLotteryConfiguration.FilePath, target, false);
        }
    }
}
