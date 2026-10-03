using System;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using Game.Network;
using Game.SO;
using Game.UI.Gifts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XLua;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class CigarGiftTests
    {
        private const BindingFlags InternalInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void CatalogAndBundleResolveEveryResource()
        {
            var catalog = CatalogConfiguration.Load();
            Assert.That(catalog.FindItem("cigar").Consumable, Is.True);
            Assert.That(catalog.FindGift("cigar").Effect, Is.EqualTo("gifts/cigar"));
            Assert.That(catalog.FindOffer("cigar").Price, Is.EqualTo(100));
            var gift = AssetDatabase.LoadAssetAtPath<GiftEffectSO>("Assets/RemoteContent/gifts/Cigar.asset");
            gift.ReloadFromDisk();
            Assert.That(gift.TryDefine(out var script), Is.True, gift.ScriptError);
            Assert.That(script.Duration, Is.LessThan(5));
            foreach (var resource in script.Resources)
                Assert.That(gift.FindResource(resource), Is.Not.Null, resource);
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Gifts/cigar.png"), Is.Not.Null);
        }

        [TestCase(0, 0)]
        [TestCase(-840, 430)]
        [TestCase(840, -430)]
        public void TimelineBuildsAndRewindsAtCenterAndScreenEdges(float x, float y)
        {
            var root = new GameObject("Cigar test canvas", typeof(RectTransform), typeof(Canvas));
            Sequence sequence = null;
            GiftScriptHost host = null;
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(1920, 1080);
                var target = new GameObject("Target", typeof(RectTransform)).GetComponent<RectTransform>();
                target.SetParent(rect, false);
                target.sizeDelta = new Vector2(260, 110);
                target.anchoredPosition = new Vector2(x, y);
                var layer = new GameObject("Visual", typeof(RectTransform)).GetComponent<RectTransform>();
                layer.SetParent(rect, false);
                var gift = AssetDatabase.LoadAssetAtPath<GiftEffectSO>("Assets/RemoteContent/gifts/Cigar.asset");
                gift.ReloadFromDisk();
                Assert.That(gift.TryDefine(out var script), Is.True, gift.ScriptError);
                host = (GiftScriptHost)Activator.CreateInstance(typeof(GiftScriptHost), InternalInstance, null,
                    new object[] { rect, layer, target, target, new Func<string, Object>(gift.FindResource), 20241 }, null);
                typeof(GiftScriptRuntime).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { script, host });
                sequence = (Sequence)typeof(GiftScriptHost).GetMethod("Finish", InternalInstance).Invoke(host,
                    new object[] { new Func<bool>(() => true), new Action<AudioClip, float>((clip, volume) => { }),
                        new Action<LuaFunction>(callback => { }) });
                Assert.That(sequence.Duration(), Is.EqualTo(script.Duration).Within(0.001f));
                Transform Node(string name) => layer.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
                sequence.Goto(1.23f, false);
                var mouth = Node("Mouth anchored cigar");
                var mouthPosition = mouth.position;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, mouth.localEulerAngles.z)), Is.GreaterThan(20));
                var exposedEnd = mouth.TransformPoint(new Vector3(186, 0));
                Assert.That(exposedEnd.x, Is.GreaterThan(mouthPosition.x), "Cigar must face right at every seat.");
                var cutterOpening = Node("Guillotine").TransformPoint(new Vector3(-1.1f, 13));
                Assert.That(Vector3.Distance(exposedEnd, cutterOpening), Is.LessThan(0.01f));
                sequence.Goto(3.75f, false);
                Assert.That(Vector3.Distance(mouthPosition, mouth.position), Is.LessThan(0.01f));
                Assert.That(Node("Cut cigar").gameObject.activeSelf, Is.True);
                Assert.That(Node("Uncut cigar").gameObject.activeSelf, Is.False);
                var smokePosition = Node("White smoke 3").localPosition;
                sequence.Goto(0.8f, false);
                Assert.That(Node("Uncut cigar").gameObject.activeSelf, Is.True);
                Assert.That(Node("Cut cigar").gameObject.activeSelf, Is.False);
                sequence.Goto(3.75f, false);
                Assert.That(Vector3.Distance(Node("White smoke 3").localPosition, smokePosition), Is.LessThan(0.01f));
                Assert.That(layer.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), Is.True);
                sequence.Goto(script.Duration, false);
                Assert.That(Node("Cigar stage").GetComponent<CanvasGroup>().alpha, Is.LessThan(0.001f));
            }
            finally
            {
                sequence?.Kill();
                if (host != null) typeof(GiftScriptHost).GetMethod("Dispose", InternalInstance).Invoke(host, null);
                Object.DestroyImmediate(root);
            }
        }
    }
}
