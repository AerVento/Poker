using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Framework.Configs;

namespace Game.Network
{
    public static class SlotLotteryConfiguration
    {
        public static string FilePath => Path.Combine(ConfigManager.GetDirectory("SlotMachine"), "Lottery.json");
        public static SlotLotteryCatalog Load(Func<string, uint?> itemLimit)
        {
            var text = ConfigManager.ReadAllTextAtPath(FilePath);
            return Parse(text, itemLimit);
        }

        public static SlotLotteryCatalog Parse(string text, Func<string, uint?> itemLimit)
        {
            var data = ConfigManager.Deserialize<LotteryConfigData>(text);
            var catalog = ConfigurationValidation.Build(data, itemLimit);
            using var hash = SHA256.Create();
            catalog.Revision = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));
            return catalog;
        }

    }
}
