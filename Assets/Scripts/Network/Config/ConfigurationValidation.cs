using System;
using System.IO;
using System.Linq;
using Framework.Configs;

namespace Game.Network
{
    /// <summary>Business validation and construction happen after ConfigManager has loaded the data.</summary>
    public static class ConfigurationValidation
    {
        public static void Validate(ClientNetworkConfigData data)
        {
            var host = data.ServerHost;
            if (string.IsNullOrWhiteSpace(host) || host.Trim() != host ||
                host.IndexOfAny(new[] { '/', '\\', '@', '?', '#', ' ', '\t', '\r', '\n' }) >= 0 ||
                Uri.CheckHostName(host) == UriHostNameType.Unknown)
                throw new InvalidDataException("server-host must be a host name or IP address without a scheme, port or path.");
            Port(data.ServerPort, "server-port");
            if (data.ConnectTimeoutSeconds < 1 || data.ConnectTimeoutSeconds > 300)
                throw new InvalidDataException("connect-timeout-seconds must be 1..300.");
        }

        public static void Validate(ServerNetworkConfigData data)
        {
            Port(data.ListenPort, "listen-port");
            if (data.MaxConnections < 1) throw new InvalidDataException("max-connections must be positive.");
            if (data.UseSsl && (string.IsNullOrWhiteSpace(data.Path) || !Path.IsPathFullyQualified(data.Path)))
                throw new InvalidDataException("path must be an absolute certificate file path when use-ssl is true.");
        }

        private static void Port(int port, string field)
        {
            if (port < 1 || port > ushort.MaxValue) throw new InvalidDataException(field + " must be 1..65535.");
        }

        public static ClientNetworkConfiguration Build(ClientNetworkConfigData data)
        {
            Validate(data);
            return new ClientNetworkConfiguration(data.ServerHost, (ushort)data.ServerPort, data.UseSsl,
                data.ConnectTimeoutSeconds, data.ResourceBaseUrl);
        }

        public static ServerNetworkConfiguration Build(ServerNetworkConfigData data)
        {
            Validate(data);
            return new ServerNetworkConfiguration((ushort)data.ListenPort, data.UseSsl, data.Path, data.Password, data.MaxConnections);
        }

        public static T Map<T>(object data) => ConfigManager.Deserialize<T>(ConfigManager.Serialize(data));

        public static Reward ValidateStarter(StarterGrantsConfigData data, GameCatalog catalog)
        {
            var reward = RewardConfig.ToReward(data);
            reward.Validate(catalog.MaxQuantityOf);
            foreach (var entry in reward.Entries)
                if (entry.Kind == RewardItemKind.Item && entry.Quantity > catalog.MaxQuantityOf(entry.ItemId))
                    throw new InvalidDataException("Item " + entry.ItemId + " exceeds its max-quantity.");
            if (reward.Dollars > uint.MaxValue || reward.Chips > uint.MaxValue)
                throw new InvalidDataException("Starting balances must fit in an unsigned 32-bit integer.");
            return reward;
        }

        public static SlotLotteryCatalog Build(LotteryConfigData data, Func<string, uint?> itemLimit)
        {
            SlotRewardPool Pool(LotteryPoolConfigData pool)
            {
                if (pool == null || pool.Rewards == null || pool.Rewards.Any(entry => entry == null || entry.Reward == null))
                    throw new InvalidDataException("Lottery pools and rewards cannot be null.");
                return new SlotRewardPool
                {
                    Weight = pool.Weight,
                    Rewards = pool.Rewards.Select(entry => new SlotRewardEntry
                    {
                        Weight = entry.Weight, Reward = RewardConfig.ToReward(entry.Reward)
                    }).ToArray()
                };
            }
            var result = new SlotLotteryCatalog
            {
                Enabled = data.Enabled, CostDollars = data.CostDollars, NoneWeight = data.NoneWeight, PityDraws = data.PityDraws,
                Small = Pool(data.Small), Medium = Pool(data.Medium), Grand = Pool(data.Grand)
            };
            result.Validate(itemLimit);
            return result;
        }
    }
}
