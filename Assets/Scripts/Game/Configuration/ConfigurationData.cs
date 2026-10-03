using System;
using System.Collections.Generic;
using System.Linq;
using Framework.Configs;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Scripting;

namespace Game
{
    [Preserve]
    [Config(Path = "Network/client-network.json", Usage = ConfigUsage.Client)]
    public sealed class ClientNetworkConfigData
    {
        [Header("服务器连接")]
        [Tooltip("域名或 IP，不包含协议、端口和路径。")]
        [JsonProperty(Required = Required.Always)]
        public string ServerHost = "127.0.0.1";
        [Tooltip("连接端口，1～65535。")]
        [JsonProperty(Required = Required.Always)]
        public int ServerPort = 7777;
        [Tooltip("HTTPS 页面必须使用 WSS。")]
        [JsonProperty(Required = Required.Always)]
        public bool UseSsl = false;
        [Tooltip("连接超时秒数，1～300。")]
        [JsonProperty(Required = Required.Always)]
        public int ConnectTimeoutSeconds = 10;
        [Header("远程内容")]
        [Tooltip("远程内容根地址。")]
        [JsonProperty(Required = Required.Always)]
        public string ResourceBaseUrl = "";
    }

    [Preserve]
    [Config(Path = "Network/server-network.json", Usage = ConfigUsage.Server)]
    public sealed class ServerNetworkConfigData
    {
        [Header("监听设置")]
        [Tooltip("监听端口，1～65535。")]
        [JsonProperty(Required = Required.Always)]
        public int ListenPort = 7777;
        [Tooltip("由游戏进程提供 TLS。")]
        [JsonProperty(Required = Required.Always)]
        public bool UseSsl = false;
        [Header("SSL 证书")]
        [Tooltip("PFX/P12 证书的绝对路径。")]
        [JsonProperty(Required = Required.Always)]
        public string Path = "";
        [Tooltip("证书密码。")]
        [JsonProperty(Required = Required.Always)]
        public string Password = "";
        [Header("连接限制")]
        [Tooltip("服务器总连接数上限。")]
        [JsonProperty(Required = Required.Always)]
        public int MaxConnections = 100;
    }

    [Preserve]
    public sealed class ItemConfigEntry
    {
        [Header("物品")]
        [Tooltip("稳定物品 ID。")]
        [JsonProperty(Required = Required.Always)]
        public string Id = "";
        [Tooltip("显示名称。")]
        [JsonProperty(Required = Required.Always)]
        public string Name = "";
        [Tooltip("物品说明。")]
        [JsonProperty(Required = Required.Always)]
        public string Description = "";
        [Tooltip("远程图标集中的 Sprite 名称。")]
        [JsonProperty(Required = Required.Always)]
        public string Icon = "";
        [Header("持有规则")]
        [Tooltip("是否可以消耗。")]
        [JsonProperty(Required = Required.Always)]
        public bool Consumable = false;
        [Tooltip("最大持有数量。")]
        [JsonProperty(Required = Required.Always)]
        public uint MaxQuantity = 1;
    }

    [Preserve]
    public sealed class NamecardConfigEntry
    {
        [Tooltip("关联的名片物品 ID。")]
        [JsonProperty(Required = Required.Always)]
        [ItemId]
        public string ItemId = "";
        [Tooltip("远程名片地址；默认名片为空。")]
        [JsonProperty(Required = Required.Always)]
        public string Artwork = "";
    }

    [Preserve]
    public sealed class CardFaceConfigEntry
    {
        [Tooltip("关联的牌面物品 ID。")]
        [JsonProperty(Required = Required.Always)]
        [ItemId]
        public string ItemId = "";
        [Tooltip("远程牌面地址；经典牌面为空。")]
        [JsonProperty(Required = Required.Always)]
        public string Cards = "";
        [Tooltip("预览图名称。")]
        [JsonProperty(Required = Required.Always)]
        public string Preview = "";
    }

    [Preserve]
    public sealed class GiftConfigEntry
    {
        [Tooltip("关联的礼物物品 ID。")]
        [JsonProperty(Required = Required.Always)]
        [ItemId]
        public string ItemId = "";
        [Tooltip("远程礼物特效地址。")]
        [JsonProperty(Required = Required.Always)]
        public string Effect = "";
    }

    [Preserve]
    public sealed class ShopConfigEntry
    {
        [Header("商品")]
        [Tooltip("关联的物品 ID。")]
        [JsonProperty(Required = Required.Always)]
        [ItemId]
        public string ItemId = "";
        [Tooltip("购买价格。")]
        [JsonProperty(Required = Required.Always)]
        public uint Price = 0;
        [Header("上架设置")]
        [Tooltip("是否在商城显示。")]
        [JsonProperty(Required = Required.Always)]
        public bool Listed = true;
        [Tooltip("是否允许购买。")]
        [JsonProperty(Required = Required.Always)]
        public bool Purchasable = true;
    }

    [Preserve]
    [Config(Path = "Items/Items.json", Usage = ConfigUsage.Server)]
    public sealed class ItemsConfigData : List<ItemConfigEntry>
    {
    }

    [Preserve]
    [Config(Path = "Items/Namecards.json", Usage = ConfigUsage.Server)]
    public sealed class NamecardsConfigData : List<NamecardConfigEntry>
    {
    }

    [Preserve]
    [Config(Path = "Items/CardFaces.json", Usage = ConfigUsage.Server)]
    public sealed class CardFacesConfigData : List<CardFaceConfigEntry>
    {
    }

    [Preserve]
    [Config(Path = "Items/Gifts.json", Usage = ConfigUsage.Server)]
    public sealed class GiftsConfigData : List<GiftConfigEntry>
    {
    }

    [Preserve]
    [Config(Path = "Shop/Shop.json", Usage = ConfigUsage.Server)]
    public sealed class ShopConfigData : List<ShopConfigEntry>
    {
    }

    [Preserve]
    public sealed class ContractConfigEntry
    {
        [Tooltip("契约物品 ID。")]
        [JsonProperty(Required = Required.Always)]
        [ItemId]
        public string ItemId = "";
        [Tooltip("签约资产门槛，不含该值。")]
        [JsonProperty(Required = Required.Always)]
        public uint SignBelowAssets = 1000;
        [Tooltip("签约获得的美金与对应债务。")]
        [JsonProperty(Required = Required.Always)]
        public uint Payout = 1000;
        [Tooltip("偿还一份契约需要的美金。")]
        [JsonProperty(Required = Required.Always)]
        public uint Repayment = 1100;
        [Tooltip("一份契约对应的服役年数。")]
        [JsonProperty(Required = Required.Always)]
        public uint ServiceYears = 1;
    }

    [Preserve]
    public sealed class ServiceNamecardConfigEntry
    {
        [Tooltip("启用名片的最低服役年数。")]
        [JsonProperty(Required = Required.Always)]
        public ulong MinServiceYears = 1;
        [Tooltip("关联的名片物品 ID。")]
        [JsonProperty(Required = Required.Always)]
        [ItemId]
        public string NamecardItemId = "";
    }

    [Preserve]
    [Config(Path = "Items/Contracts.json", Usage = ConfigUsage.Server)]
    public sealed class ContractsConfigData
    {
        [Header("契约")]
        [Tooltip("按物品定义契约。")]
        [JsonProperty(Required = Required.Always)]
        public ContractConfigEntry[] Contracts = Array.Empty<ContractConfigEntry>();
        [Header("服役名片")]
        [Tooltip("服役年数与名片对应关系。")]
        [JsonProperty(Required = Required.Always)]
        public ServiceNamecardConfigEntry[] ServiceNamecards = Array.Empty<ServiceNamecardConfigEntry>();
    }

    [Preserve]
    public sealed class RewardConfigEntry
    {
        [Tooltip("奖励种类。")]
        [JsonProperty(Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public RewardItemKind Kind = RewardItemKind.Dollars;
        [Tooltip("物品奖励的物品 ID。")]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [ItemId]
        public string ItemId = null;
        [Tooltip("奖励数量。")]
        [JsonProperty(Required = Required.Always)]
        public ulong Quantity = 1;
    }

    public static class RewardConfig
    {
        public static Reward ToReward(IEnumerable<RewardConfigEntry> entries)
        {
            if (entries == null || entries.Any(entry => entry == null)) throw new System.IO.InvalidDataException("Reward entries cannot be null.");
            return new Reward(entries.Select(entry => new RewardEntry { Kind = entry.Kind, ItemId = entry.ItemId, Quantity = entry.Quantity }));
        }
    }

    /// <summary>Granted once to each new account; only affects accounts created afterwards.</summary>
    [Preserve]
    [Config(Path = "Accounts/StarterGrants.json", Usage = ConfigUsage.Server)]
    public sealed class StarterGrantsConfigData : List<RewardConfigEntry>
    {
    }

    [Preserve]
    public sealed class LotteryEntryConfigData
    {
        [Tooltip("该奖励在档位内的权重。")]
        [JsonProperty(Required = Required.Always)]
        public int Weight = 1;
        [Tooltip("该次奖励内容。")]
        [JsonProperty(Required = Required.Always)]
        public RewardConfigEntry[] Reward = Array.Empty<RewardConfigEntry>();
    }

    [Preserve]
    public sealed class LotteryPoolConfigData
    {
        [Tooltip("抽中该档位的权重；0 表示不抽取。")]
        [JsonProperty(Required = Required.Always)]
        public int Weight = 0;
        [Tooltip("该档位内的奖励。")]
        [JsonProperty(Required = Required.Always)]
        public LotteryEntryConfigData[] Rewards = Array.Empty<LotteryEntryConfigData>();
    }

    [Preserve]
    [Config(Path = "SlotMachine/Lottery.json", Usage = ConfigUsage.Server)]
    public sealed class LotteryConfigData
    {
        [Header("抽奖设置")]
        [Tooltip("是否开放抽奖。")]
        [JsonProperty(Required = Required.Always)]
        public bool Enabled = true;
        [Tooltip("单次抽奖费用。")]
        [JsonProperty(Required = Required.Always)]
        public uint CostDollars = 100;
        [Tooltip("未中奖权重。")]
        [JsonProperty(Required = Required.Always)]
        public int NoneWeight = 1;
        [Tooltip("保底抽数：连续未中奖达到该抽数减一后，下一抽必中小奖及以上；0 表示不保底。")]
        [JsonProperty(Required = Required.Always)]
        public int PityDraws = 0;
        [Header("奖池")]
        [Tooltip("小奖池。")]
        [JsonProperty(Required = Required.Always)]
        public LotteryPoolConfigData Small = new LotteryPoolConfigData();
        [Tooltip("中等奖池。")]
        [JsonProperty(Required = Required.Always)]
        public LotteryPoolConfigData Medium = new LotteryPoolConfigData();
        [Tooltip("大奖池。")]
        [JsonProperty(Required = Required.Always)]
        public LotteryPoolConfigData Grand = new LotteryPoolConfigData();
    }

    public enum TaskConfigKind
    {
        DirectClaim, DirectClaimWhenLesserThan, DirectClaimWhenAtMost, PlayHands,
        FormPattern, WinHands, WinWithPattern, CountHands, AllObjectives
    }

    public enum TaskConditionKind { Won, HandSettled, PatternObserved }
    public enum TaskObjectiveKind { CountDistinctHands }

    [Preserve]
    public sealed class TaskConfigEntry
    {
        [Header("任务")]
        [Tooltip("稳定任务 ID。")]
        [JsonProperty(Required = Required.Always)]
        public string Id = "new_task";
        [Tooltip("任务名称。")]
        [JsonProperty(Required = Required.Always)]
        public string Name = "新任务";
        [Tooltip("省略时使用规则生成的说明。")]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Description = null;
        [Tooltip("任务规则类型。")]
        [JsonProperty(Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public TaskConfigKind Type = TaskConfigKind.DirectClaim;
        [Tooltip("每完成一轮可领取的奖励。")]
        [JsonProperty(Required = Required.Always)]
        public RewardConfigEntry[] Reward = Array.Empty<RewardConfigEntry>();
        [Header("任务规则")]
        [Tooltip("参数个数与类型由任务规则决定。")]
        [JsonProperty("params", Required = Required.Always)]
        public JToken[] Parameters = Array.Empty<JToken>();
        [Tooltip("可完成次数。")]
        [JsonProperty(Required = Required.Always)]
        public int MaxCompletions = 1;
    }

    [Preserve]
    public sealed class FixedTaskConfigEntry
    {
        [Tooltip("任务 ID。")]
        [JsonProperty(Required = Required.Always)]
        [TaskId]
        public string TaskId = "";
        [Tooltip("是否启用。")]
        [JsonProperty(Required = Required.Always)]
        public bool Enabled = true;
    }

    [Preserve]
    public sealed class RandomTaskConfigEntry
    {
        [Tooltip("任务 ID。")]
        [JsonProperty(Required = Required.Always)]
        [TaskId]
        public string TaskId = "";
        [Tooltip("是否启用。")]
        [JsonProperty(Required = Required.Always)]
        public bool Enabled = true;
        [Tooltip("抽取权重。")]
        [JsonProperty(Required = Required.Always)]
        public double Weight = 1;
        [Tooltip("有效天数。")]
        [JsonProperty(Required = Required.Always)]
        public int ValidityDays = 1;
    }

    [Preserve]
    [Config(Path = "DailyTasks/TaskDefinitions.json", Usage = ConfigUsage.Server)]
    public sealed class TaskDefinitionsConfigData : List<TaskConfigEntry>
    {
    }

    /// <summary>Assigned every day in file order.</summary>
    [Preserve]
    [Config(Path = "DailyTasks/FixedTasks.json", Usage = ConfigUsage.Server)]
    public sealed class FixedTasksConfigData : List<FixedTaskConfigEntry>
    {
    }

    [Preserve]
    [Config(Path = "DailyTasks/RandomTasks.json", Usage = ConfigUsage.Server)]
    public sealed class RandomTasksConfigData
    {
        [Header("随机任务")]
        [Tooltip("随机任务名额。")]
        [JsonProperty(Required = Required.Always)]
        public int Capacity = 2;
        [Header("条目")]
        [Tooltip("任务列表。")]
        [JsonProperty(Required = Required.Always)]
        public RandomTaskConfigEntry[] Entries = Array.Empty<RandomTaskConfigEntry>();
    }

    [Preserve]
    public sealed class TaskConditionConfigData
    {
        [Tooltip("手牌条件类型。")]
        [JsonProperty(Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public TaskConditionKind Type = TaskConditionKind.Won;
        [Tooltip("参数类型由条件类型决定。")]
        [JsonProperty("params", Required = Required.Always)]
        public JToken[] Parameters = Array.Empty<JToken>();
    }

    [Preserve]
    public sealed class TaskObjectiveConfigData
    {
        [Tooltip("子目标的稳定 ID。")]
        [JsonProperty(Required = Required.Always)]
        public string Id = "new_objective";
        [JsonProperty(Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public TaskObjectiveKind Type = TaskObjectiveKind.CountDistinctHands;
        [Tooltip("目标次数与手牌条件。")]
        [JsonProperty("params", Required = Required.Always)]
        public JToken[] Parameters = new JToken[] { new JValue(1), new JArray(JObject.Parse(ConfigManager.Serialize(new TaskConditionConfigData()))) };
    }
}
