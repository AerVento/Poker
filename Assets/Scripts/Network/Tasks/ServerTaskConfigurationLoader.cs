using System;
using System.IO;
using System.Linq;
using Game.Network.Accounts;
using Game.Tasks;
using UnityEngine;

namespace Game.Network.Tasks
{
    public static class ServerTaskConfigurationLoader
    {
        public static readonly string[] FileNames = { "TaskDefinitions.json", "FixedTasks.json", "RandomTasks.json" };
        public static string DefaultDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Config", "DailyTasks"));
        public static string ConfigurationDirectory
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "--task-config");
                if (index >= 0)
                {
                    if (index + 1 >= args.Length) throw new ArgumentException("--task-config requires a directory.");
                    return Path.GetFullPath(args[index + 1]);
                }
                var configured = Environment.GetEnvironmentVariable("POKER_TASK_CONFIG_DIR");
                return string.IsNullOrEmpty(configured) ? DefaultDirectory : Path.GetFullPath(configured);
            }
        }
        public static TaskCatalog Load(ItemPurchaseRuntime items)
        {
            var directory = ConfigurationDirectory;
            try
            {
                var texts = FileNames.Select(name => File.ReadAllText(Path.Combine(directory, name))).ToArray();
                return TaskCatalog.Load(texts[0], texts[1], texts[2], id => items?.Find(id)?.MaxQuantity);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Daily task configuration failed at " + directory + ": " + error.Message, error);
            }
        }
    }
}
