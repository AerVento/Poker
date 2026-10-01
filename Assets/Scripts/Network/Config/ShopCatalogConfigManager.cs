using System;
using System.IO;
using Framework.Configs;

namespace Game.Network
{
    /// <summary>Paths of the shop configuration files in Configs/Shop; see ShopConfiguration.</summary>
    public static class ShopCatalogConfigManager
    {
        public const string ConfigurationSection = "Shop";
        public static string DefaultDirectory => ConfigManager.GetDirectory(ConfigurationSection);
        public static string ConfigurationDirectory => DefaultDirectory;

        public static string GetPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
                throw new ArgumentException("Shop configuration file name must not contain a directory.", nameof(fileName));
            return ConfigManager.GetPath(ConfigurationSection, fileName);
        }
    }
}
