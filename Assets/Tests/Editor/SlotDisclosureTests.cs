using System;
using System.Linq;
using System.Reflection;
using Game.Network;
using Game.UI;
using Mirror;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class SlotDisclosureTests
    {
        [Test]
        public void ReelPresentationVariesMissesAndPreservesWinningTiers()
        {
            var method = typeof(SlotMachineView).GetMethod("CreateReelSymbols", BindingFlags.NonPublic | BindingFlags.Static);
            var state = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(1729);
                var seen = new System.Collections.Generic.HashSet<string>();
                for (int i = 0; i < 2048; i++)
                {
                    var symbols = (int[])method.Invoke(null, new object[] { SlotPrizeTier.None });
                    Assert.AreEqual(3, symbols.Length);
                    Assert.IsTrue(symbols.All(symbol => symbol >= 0 && symbol < 3));
                    Assert.IsFalse(symbols.All(symbol => symbol == symbols[0]));
                    seen.Add(string.Join(",", symbols));
                }
                Assert.AreEqual(24, seen.Count);
                foreach (var tier in new[] { SlotPrizeTier.Small, SlotPrizeTier.Medium, SlotPrizeTier.Grand })
                    CollectionAssert.AreEqual(Enumerable.Repeat((int)tier - 1, 3),
                        (int[])method.Invoke(null, new object[] { tier }));
            }
            finally { UnityEngine.Random.state = state; }
        }

        private static SlotLotteryCatalog Catalog(int count = 3)
        {
            return new SlotLotteryCatalog
            {
                NoneWeight = 60,
                Small = new SlotRewardPool { Weight = 30, Rewards = Enumerable.Range(0, count).Select(i =>
                    new SlotRewardEntry { Weight = i == 0 ? 60 : i == 1 ? 30 : 10,
                        Reward = new Reward(new[] { new RewardEntry { Kind = RewardItemKind.Dollars, Quantity = (ulong)(80 + i) } }) }).ToArray() },
                Medium = new SlotRewardPool { Weight = 9 }, Grand = new SlotRewardPool { Weight = 1 }
            };
        }

        [Test]
        public void DisclosureUsesTierAndWholePoolWeights()
        {
            var disclosure = SlotLotteryDisclosure.From(Catalog(), SlotPrizeTier.Small);
            Assert.AreEqual(.3, disclosure.Probability(SlotPrizeTier.Small), 1e-12);
            Assert.AreEqual(.6, disclosure.Probability(SlotPrizeTier.None), 1e-12);
            Assert.AreEqual(.6, disclosure.PoolProbability(disclosure.Rewards[0]), 1e-12);
            Assert.AreEqual(.18, disclosure.Probability(disclosure.Tier) * disclosure.PoolProbability(disclosure.Rewards[0]), 1e-12);
            Assert.AreEqual("18%", SlotProbabilityText.Format(.18));
            Assert.AreNotEqual("0%", SlotProbabilityText.Format(1e-12));
            Assert.AreEqual("0%", SlotProbabilityText.Format(0));
        }

        [Test]
        public void PagingCoversEveryRewardWithoutChangingProbabilityDenominator()
        {
            var catalog = Catalog(200);
            var entries = Enumerable.Range(0, 34).SelectMany(page =>
            {
                var data = SlotLotteryDisclosure.From(catalog, SlotPrizeTier.Small, page * SlotLotteryDisclosure.PageSize);
                Assert.AreEqual(2070, data.PoolWeight);
                Assert.AreEqual(200, data.TotalRewards);
                Assert.LessOrEqual(data.Rewards.Length, SlotLotteryDisclosure.PageSize);
                return data.Rewards.Select(r => r.Reward.Entries[0].Quantity);
            }).ToArray();
            CollectionAssert.AreEqual(catalog.Small.Rewards.Select(r => r.Reward.Entries[0].Quantity), entries);
            Assert.AreEqual(198, SlotLotteryDisclosure.From(catalog, SlotPrizeTier.Small, 199).Offset);
            Assert.IsEmpty(SlotLotteryDisclosure.From(catalog).Rewards);
            catalog.Small.Weight = 0;
            Assert.AreEqual(0, SlotLotteryDisclosure.From(catalog, SlotPrizeTier.Small).Probability(SlotPrizeTier.Small));
        }

        [Test]
        public void MirrorRoundTripsPoolRequestAndBoundedDisclosure()
        {
            using var writer = NetworkWriterPool.Get();
            writer.Write(new SlotLotteryRequest { RequestId = 7, Action = SlotLotteryAction.Pool, Tier = SlotPrizeTier.Small, Offset = 198 });
            var original = SlotLotteryDisclosure.From(Catalog(200), SlotPrizeTier.Small, 198);
            writer.Write(new SlotLotteryResponse { RequestId = 7, Disclosure = original });
            Assert.Less(writer.Position, 16000);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            var request = reader.Read<SlotLotteryRequest>();
            var response = reader.Read<SlotLotteryResponse>();
            Assert.AreEqual(SlotLotteryAction.Pool, request.Action);
            Assert.AreEqual(SlotPrizeTier.Small, request.Tier);
            Assert.AreEqual(198, request.Offset);
            Assert.AreEqual(original.PoolWeight, response.Disclosure.PoolWeight);
            Assert.AreEqual(original.Rewards[1].Reward.Entries[0].Quantity, response.Disclosure.Rewards[1].Reward.Entries[0].Quantity);
            Assert.AreEqual(original.Probability(SlotPrizeTier.Small), response.Disclosure.Probability(SlotPrizeTier.Small));
        }

        [Test]
        public void WalletNotifiesAfterAtomicUpdatesAndSkipsUnchangedValues()
        {
            var state = new GameSettings();
            int calls = 0;
            state.WalletChanged += () => calls++;
            state.SetAuthenticatedAccount("id", "name", 40, null, "token", 100);
            state.SetWallet(100, 40);
            Assert.AreEqual(1, calls);
            state.SetWallet(101, 40);
            state.SetChips(41);
            state.SetDollars(102);
            Assert.AreEqual(4, calls);
            state.WalletChanged += () => { Assert.AreEqual(0, state.Dollars); Assert.AreEqual(0, state.Chips); Assert.IsFalse(state.IsAuthenticated); };
            state.ClearAuthenticatedAccount();
            Assert.AreEqual(5, calls);
        }

        [Test]
        public void MigratedPanelsHaveSharedBalanceAndValidBindings()
        {
            foreach (var name in new[] { "LobbyPanel", "RoomListPanel", "RoomPanel", "GamePanel", "ShopPanel", "ChipExchangePanel", "SlotMachinePanel" })
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/UI/Panels/{name}/{name}.prefab");
                Assert.AreEqual(name == "ShopPanel" ? 1 : 2, root.GetComponentsInChildren<BalanceView>(true).Length, name);
                CheckReferences(root, typeof(BalanceView), typeof(GamePanelControls), typeof(ChipExchangePanel), typeof(SlotMachinePanel));
            }
            foreach (var name in new[] { "SlotPrizePoolPanel", "SlotPoolRewardView" })
                CheckReferences(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/UI/Panels/SlotPrizePoolPanel/{name}.prefab"),
                    typeof(SlotPrizePoolPanel), typeof(SlotPoolRewardView));
            var manager = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Manager/UIManager.prefab").GetComponent<Framework.UI.UIManager>());
            var panels = manager.FindProperty("_panels");
            Assert.AreEqual(1, Enumerable.Range(0, panels.arraySize).Count(i =>
                (panels.GetArrayElementAtIndex(i).objectReferenceValue as GameObject)?.GetComponent<SlotPrizePoolPanel>() != null));
        }

        private static void CheckReferences(GameObject root, params Type[] types)
        {
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Assert.IsNotNull(component, root.name + " missing script");
                if (!types.Contains(component.GetType())) continue;
                foreach (var field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (!field.IsDefined(typeof(SerializeField), true)) continue;
                    if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                        Assert.IsTrue((UnityEngine.Object)field.GetValue(component) != null, root.name + " " + field.Name);
                    if (field.FieldType.IsArray && typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType.GetElementType()))
                    {
                        var array = (UnityEngine.Object[])field.GetValue(component);
                        Assert.AreEqual(3, array.Length);
                        Assert.IsTrue(array.All(value => value != null), field.Name);
                    }
                }
            }
        }
    }
}
