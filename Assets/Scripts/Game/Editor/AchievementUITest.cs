using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Framework.Singleton;
using Framework.UI;
using Game.Achievements;
using Game.Network;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class AchievementUITest
    {
        private const string Directory = "Assets/Prefabs/UI/Panels/";
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        [Test]
        public void PrefabsAreRegisteredWithCompleteReferencesAndVerticalScrollView()
        {
            var manager = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Manager/UIManager.prefab").GetComponent<UIManager>();
            var prefabs = Field<GameObject[]>(manager, "_panels");
            foreach (var type in new[] { typeof(AchievementPanel), typeof(AchievementBannerPanel) })
                Assert.AreEqual(1, prefabs.Count(p => p.GetComponent(type) != null));
            foreach (var name in new[] { "AchievementPanel", "AchievementBannerPanel", "AchievementRow" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Directory + "AchievementPanel/" + name + ".prefab");
                Assert.NotNull(prefab);
                foreach (var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    Assert.NotNull(behaviour, "Missing script in " + name);
                    if (!behaviour.GetType().Name.StartsWith("Achievement", StringComparison.Ordinal)) continue;
                    var serialized = new SerializedObject(behaviour);
                    var property = serialized.GetIterator();
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference)
                            Assert.NotNull(property.objectReferenceValue, name + ":" + property.propertyPath);
                }
            }
            var panel = AssetDatabase.LoadAssetAtPath<GameObject>(Directory + "AchievementPanel/AchievementPanel.prefab").GetComponent<AchievementPanel>();
            var scroll = Field<ScrollRect>(panel, "_scroll");
            Assert.IsTrue(scroll.vertical); Assert.IsFalse(scroll.horizontal);
            Assert.NotNull(scroll.viewport.GetComponent<RectMask2D>());
            Assert.NotNull(scroll.content.GetComponent<VerticalLayoutGroup>());
            Assert.AreEqual(ContentSizeFitter.FitMode.PreferredSize, scroll.content.GetComponent<ContentSizeFitter>().verticalFit);
            Assert.NotNull(Field<Button>(AssetDatabase.LoadAssetAtPath<GameObject>(Directory + "TitlePanel/TitlePanel.prefab").GetComponent<TitlePanel>(), "_achievements"));
            foreach (var achievement in AchievementCatalog.Discover().All)
                Assert.NotNull(Resources.Load<Sprite>(achievement.IconPath), achievement.Id);
            Assert.NotNull(AchievementRow.LoadIcon("does-not-exist"));
        }

        [Test]
        public void CategoryFilteringShowsOneRowPerAchievementAndCreatesNewCategoryAutomatically()
        {
            var root = new GameObject("Achievement UI test");
            root.SetActive(false);
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Directory + "AchievementPanel/AchievementPanel.prefab"), root.transform);
            try
            {
                var panel = instance.GetComponent<AchievementPanel>();
                var views = Enumerable.Range(0, 15).Select(i => new AchievementView
                {
                    Id = "test_" + i, Name = "成就 " + i, Description = "列表测试", IconPath = "UI/Achievements/Default",
                    Target = 1, Category = i < 7 ? AchievementCategories.General : i < 14 ? AchievementCategories.Special : "探索"
                }).ToArray();
                panel.Present(views, false);
                Assert.AreEqual(15, Field<List<AchievementRow>>(panel, "_rows").Count);
                Assert.AreEqual(4, Field<List<Button>>(panel, "_tabButtons").Count);
                panel.SelectCategory(AchievementCategories.Special);
                Assert.AreEqual(7, Field<List<AchievementRow>>(panel, "_rows").Count);
                panel.SelectCategory("探索");
                Assert.AreEqual(1, Field<List<AchievementRow>>(panel, "_rows").Count);
                panel.SelectCategory(null);
                Assert.AreEqual(15, Field<List<AchievementRow>>(panel, "_rows").Count);
                Assert.AreEqual(15, Field<ScrollRect>(panel, "_scroll").content.childCount);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AchievementBannerDoesNotStealNavigationOrDisableUnderlyingPanel()
        {
            var instanceField = typeof(MonoSingleton<UIManager>).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            var previous = instanceField.GetValue(null);
            var root = new GameObject("Achievement navigation test");
            root.SetActive(false);
            try
            {
                var ui = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Manager/UIManager.prefab"), root.transform).GetComponent<UIManager>();
                instanceField.SetValue(null, ui);
                foreach (var name in new[] { "_bottomLayer", "_middleLayer", "_topLayer", "_systemLayer" })
                {
                    var layer = new GameObject(name, typeof(RectTransform)); layer.transform.SetParent(ui.transform);
                    typeof(UIManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ui, layer.transform);
                }
                typeof(UIManager).GetMethod("InitializeRuntimePanel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, null);
                var basic = new GameObject("Basic", typeof(RectTransform), typeof(BasePanel)); basic.transform.SetParent(root.transform);
                Field<Dictionary<Type, GameObject>>(ui, "_runtimePanels").Add(typeof(BasePanel), basic);
                var table = ui.PushPanel<BasePanel>("Table");
                var banner = ui.PushPanel<AchievementBannerPanel>(UIManager.Layer.System);
                Assert.AreEqual("Table", ui.TopPanelIdentifier);
                Assert.IsTrue(table.GetComponent<CanvasGroup>().interactable);
                Assert.IsTrue(table.GetComponent<CanvasGroup>().blocksRaycasts);
                Assert.IsFalse(banner.GetComponent<CanvasGroup>().blocksRaycasts);
                ui.DestroyPanel(nameof(AchievementBannerPanel));
                Assert.AreEqual("Table", ui.TopPanelIdentifier);
            }
            finally { Object.DestroyImmediate(root); instanceField.SetValue(null, previous); }
        }
    }
}
