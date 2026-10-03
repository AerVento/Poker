using System;
using System.IO;
using System.Linq;
using Framework.Configs;
using Game.Tasks;

namespace Game.Network.Tasks
{
    public static class ServerTaskConfigurationLoader
    {
        public static readonly string[] FileNames = { "TaskDefinitions.json", "FixedTasks.json", "RandomTasks.json" };
        public static string DefaultDirectory => ConfigManager.GetDirectory("DailyTasks");
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
        public static TaskCatalog Load(Func<string, uint?> itemLimit)
        {
            var directory = ConfigurationDirectory;
            try
            {
                return TaskCatalog.Build(
                    ConfigManager.LoadAtPath<TaskDefinitionsConfigData>(Path.Combine(directory, FileNames[0])),
                    ConfigManager.LoadAtPath<FixedTasksConfigData>(Path.Combine(directory, FileNames[1])),
                    ConfigManager.LoadAtPath<RandomTasksConfigData>(Path.Combine(directory, FileNames[2])), itemLimit);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Daily task configuration failed at " + directory + ": " + error.Message, error);
            }
        }
    }
}
