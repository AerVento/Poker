using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Framework.Configs;
using Game.Network.Accounts;
using Game.Network.Tasks;
using Game.Tasks;
using UnityEngine;

namespace Game.Network
{
    /// <summary>
    /// Every server configuration that refers to items: the item and shop catalogs, starter grants, daily tasks and
    /// the slot lottery. All files are read and validated together so one startup reports every problem at once.
    /// </summary>
    public sealed class ServerConfiguration
    {
        public const string AccountsSection = "Accounts";
        public const string StarterGrantsFile = "StarterGrants.json";

        public GameCatalog Catalog { get; }
        /// <summary>Granted once to each new account, in the account creation transaction.</summary>
        public Reward StarterGrants { get; }
        public TaskCatalog Tasks { get; }
        public SlotLotteryCatalog SlotLottery { get; }

        private ServerConfiguration(GameCatalog catalog, Reward starterGrants, TaskCatalog tasks, SlotLotteryCatalog slotLottery)
        {
            Catalog = catalog;
            StarterGrants = starterGrants;
            Tasks = tasks;
            SlotLottery = slotLottery;
        }

        private static ServerConfiguration _current;

        /// <summary>The configuration this server process uses; loaded on first use and throws when invalid.</summary>
        public static ServerConfiguration Current => _current ??= Load();

        public static string StarterGrantsPath => ConfigManager.GetPath(AccountsSection, StarterGrantsFile);

        public static ServerConfiguration Load()
        {
            var errors = new List<string>();
            var configuration = TryLoad(errors);
            if (errors.Count > 0)
                throw new InvalidDataException(errors.Count + " configuration problem(s):\n" + string.Join("\n", errors));
            return configuration;
        }

        /// <summary>Loads every file it can and adds each problem to errors; returns null when anything failed.</summary>
        public static ServerConfiguration TryLoad(List<string> errors, bool logSuccess = true)
        {
            var before = errors.Count;
            var catalog = CatalogConfiguration.TryLoad(errors);
            if (catalog == null)
            {
                // Other files refer to items; without a valid catalog their item checks would only repeat the same failure.
                errors.Add("Starter grants, daily tasks and the slot lottery were not checked because the item catalog is invalid.");
                return null;
            }
            var starter = Capture(errors, StarterGrantsPath, () => LoadStarterGrants(catalog));
            var tasks = Capture(errors, ServerTaskConfigurationLoader.ConfigurationDirectory, () => ServerTaskConfigurationLoader.Load(catalog.MaxQuantityOf));
            var slot = Capture(errors, SlotLotteryConfiguration.FilePath, () => SlotLotteryConfiguration.Load(catalog.MaxQuantityOf));
            if (errors.Count > before) return null;
            if (logSuccess)
                NetworkConfigurationLoader.LogInfo("Server configuration validated: catalog " + catalog.Revision +
                    ", starter grants " + starter.Entries.Count + " entries.");
            return new ServerConfiguration(catalog, starter, tasks, slot);
        }

        public static Reward LoadStarterGrants(GameCatalog catalog)
        {
            var data = ConfigManager.Load<StarterGrantsConfigData>();
            return ConfigurationValidation.ValidateStarter(data, catalog);
        }

        private static T Capture<T>(List<string> errors, string source, Func<T> load) where T : class
        {
            try { return load(); }
            catch (Exception exception)
            {
                errors.Add(source + ": " + exception.Message);
                return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _current = null;
    }
}
