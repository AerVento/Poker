using System.IO;
using Framework.Configs;
using Game.Network;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Editor
{
    public sealed class ShopCatalogValidationBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report) => Validate();

        // The same validation the server runs at startup.
        [MenuItem("Poker/Shop/Validate Catalogs")]
        public static void Validate() => ShopConfiguration.Load();
    }

    public sealed class ShopCatalogConfigurationBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
                report.summary.platform != BuildTarget.StandaloneLinux64)
                return;

            var destination = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "Configs", "Shop");
            Directory.CreateDirectory(destination);
            foreach (var fileName in new[] { ShopConfiguration.ItemsFile, ShopConfiguration.GiftsFile,
                ShopConfiguration.NamecardsFile, ShopConfiguration.CardFacesFile })
            {
                var target = Path.Combine(destination, fileName);
                if (!ConfigManager.FileExists(ShopCatalogConfigManager.ConfigurationSection, fileName) || File.Exists(target))
                    continue;
                ConfigManager.CopyFileAtPath(ShopCatalogConfigManager.GetPath(fileName), target, false);
            }
        }
    }
}
