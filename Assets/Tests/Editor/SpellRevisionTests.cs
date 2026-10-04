using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using Game.SO;
using Game.UI.Gifts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XLua;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class SpellRevisionTests
    {
        private const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Playback : IDisposable
        {
            public GameObject Root;
            public RectTransform Layer;
            public GiftScriptHost Host;
            public Sequence Timeline;
            public Playback(string name, int seed, Transform parent = null)
            {
                Root = new GameObject("Spell", typeof(RectTransform));
                if (parent == null) Root.AddComponent<Canvas>();
                else Root.transform.SetParent(parent, false);
                var rect = (RectTransform)Root.transform;
                rect.sizeDelta = new Vector2(1920, 1080);
                var target = new GameObject("Target", typeof(RectTransform)).GetComponent<RectTransform>();
                target.SetParent(rect, false);target.anchoredPosition = new Vector2(650, 180);target.sizeDelta = new Vector2(200, 100);
                Layer = new GameObject("Visual", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
                Layer.SetParent(rect, false);
                var asset = AssetDatabase.LoadAssetAtPath<GiftEffectSO>("Assets/RemoteContent/gifts/" + name + ".asset");
                asset.ReloadFromDisk();
                Assert.That(asset.TryDefine(out var script), Is.True, asset.ScriptError);
                Host = (GiftScriptHost)Activator.CreateInstance(typeof(GiftScriptHost), Internal, null,
                    new object[] { rect, Layer, target, target, new Func<string, Object>(asset.FindResource), seed }, null);
                typeof(GiftScriptRuntime).GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { script, Host });
                Timeline = (Sequence)typeof(GiftScriptHost).GetMethod("Finish", Internal).Invoke(Host,
                    new object[] { new Func<bool>(() => true), new Action<AudioClip, float>((c, v) => { }), new Action<LuaFunction>(f => { }) });
            }
            public RectTransform[] Named(string prefix) => Layer.GetComponentsInChildren<RectTransform>(true).Where(n => n.name.StartsWith(prefix)).ToArray();
            public void Dispose()
            {
                Timeline?.Kill();
                typeof(GiftScriptHost).GetMethod("Dispose", Internal).Invoke(Host, null);
                Object.DestroyImmediate(Root);
            }
        }

        [Test]
        public void CannonReachesFullWidthNearEmitterThenStaysCylindrical()
        {
            var root = new GameObject("Cannon", typeof(RectTransform));
            try
            {
                var graphic = root.AddComponent<SpellBulletGraphic>();
                graphic.Kind = SpellBulletGraphic.Shape.Cannon;
                graphic.rectTransform.sizeDelta = new Vector2(1000, 200);
                using var vertices = new UnityEngine.UI.VertexHelper();
                typeof(SpellBulletGraphic).GetMethod("OnPopulateMesh", Internal, null, new[] { typeof(UnityEngine.UI.VertexHelper) }, null).Invoke(graphic, new object[] { vertices });
                var heights = new Dictionary<int, float>();
                var vertex = new UnityEngine.UIVertex();
                for (int i = 0; i < vertices.currentVertCount; i++)
                {
                    vertices.PopulateUIVertex(ref vertex, i);
                    int x = Mathf.RoundToInt(vertex.position.x + 500);
                    heights[x] = Mathf.Max(heights.TryGetValue(x, out float height) ? height : 0, Mathf.Abs(vertex.position.y));
                }
                Assert.That(heights[0], Is.LessThan(12));
                Assert.That(heights[110], Is.EqualTo(100).Within(.01f));
                foreach (var pair in heights.Where(p => p.Key >= 110)) Assert.That(pair.Value, Is.EqualTo(100).Within(.01f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SummonedProjectilesAlreadyHaveVariedRotation()
        {
            using var playback = new Playback("HourOfDaybreak", 204);
            playback.Timeline.Goto(2f, false);
            foreach (var prefix in new[] { "Dawn star ", "Dawn ofuda ", "Dawn butterfly " })
            {
                var bullets = playback.Named(prefix);
                Assert.That(bullets.Select(n => Mathf.RoundToInt(n.localEulerAngles.z / 20)).Distinct().Count(), Is.GreaterThan(7));
                var angles = bullets.Select(n => n.localEulerAngles.z).ToArray();
                playback.Timeline.Goto(0, false);playback.Timeline.Goto(2f, false);
                Assert.That(bullets.Select(n => n.localEulerAngles.z).ToArray(), Is.EqualTo(angles));
            }
        }

        [Test]
        public void FanRipplesExpandAlongTheTargetDirection()
        {
            using var playback = new Playback("ResurrectionButterfly", 204);
            for (int emitter = 0; emitter < 2; emitter++)
            {
                var bullets = playback.Named("Fan ripple 0-" + emitter + "-");
                playback.Timeline.Goto(1.69f, false);
                var origins = bullets.Select(n => n.anchoredPosition).ToArray();
                playback.Timeline.Goto(3.9f, false);
                for (int i = 0; i < bullets.Length; i++)
                {
                    var heading = (new Vector2(650, 180) - origins[i]).normalized;
                    var flight = (bullets[i].anchoredPosition - origins[i]).normalized;
                    Assert.That(Vector2.Dot(heading, flight), Is.GreaterThan(.82f));
                }
                Assert.That(Vector2.Distance(bullets.First().anchoredPosition, bullets.Last().anchoredPosition), Is.GreaterThan(180));
            }
        }

        [TestCase("FantasySeal", "Colored spirit orb ", 2.5f)]
        [TestCase("TheWorld", "Target orbit knife ", 3.8f)]
        [TestCase("HourOfDaybreak", "Dawn star ", 2f)]
        public void DifferentPlaybackSeedsChangeFormationButScrubbingIsStable(string gift, string prefix, float time)
        {
            using var a = new Playback(gift, 100);
            using var b = new Playback(gift, 200);
            a.Timeline.Goto(time, false);b.Timeline.Goto(time, false);
            var first = a.Named(prefix).Select(n => n.anchoredPosition).ToArray();
            var second = b.Named(prefix).Select(n => n.anchoredPosition).ToArray();
            Assert.That(first.Zip(second, Vector2.Distance).Count(d => d > 5), Is.GreaterThan(first.Length / 2));
            a.Timeline.Goto(0, false);a.Timeline.Goto(time, false);
            foreach (var pair in first.Zip(a.Named(prefix), (p, n) => Vector2.Distance(p, n.anchoredPosition)))
                Assert.That(pair, Is.LessThan(.01f));
        }

        [Test]
        public void BeamThicknessTracksOnlyActiveBeamsAndReleasesOnDispose()
        {
            var canvas = new GameObject("Shared canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                using var first = new Playback("MasterSpark", 1, canvas.transform);
                using (var second = new Playback("MasterSpark", 2, canvas.transform))
                using (var otherCanvas = new Playback("MasterSpark", 3))
                {
                    first.Timeline.Goto(3f, false);second.Timeline.Goto(1f, false);otherCanvas.Timeline.Goto(3f, false);
                    first.Timeline.Goto(3.01f, false);
                    var beam = first.Named("Piercing Master Spark ")[0];
                    float single = beam.localScale.y;
                    second.Timeline.Goto(3f, false);first.Timeline.Goto(3.01f, false);
                    Assert.That(beam.localScale.y / single, Is.EqualTo(1.22f).Within(.001f), "Five colored layers still count as one beam.");
                    second.Timeline.Goto(5.1f, false);first.Timeline.Goto(3.01f, false);
                    Assert.That(beam.localScale.y, Is.EqualTo(single).Within(.001f));
                    second.Timeline.Goto(3f, false);
                }
                first.Timeline.Goto(3.01f, false);
                Assert.That(first.Named("Piercing Master Spark ")[0].localScale.y, Is.LessThan(1.1f));
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [TestCase("FantasySeal")]
        [TestCase("MasterSpark")]
        [TestCase("TheWorld")]
        [TestCase("ResurrectionButterfly")]
        [TestCase("HourOfDaybreak")]
        public void MusicPhraseOutlastsGiftAndIsSeparateFromSoundEffects(string name)
        {
            using var playback = new Playback(name, 1);
            var asset = AssetDatabase.LoadAssetAtPath<GiftEffectSO>("Assets/RemoteContent/gifts/" + name + ".asset");
            var music = asset.FindResource("themeMusic") as AudioClip;
            Assert.That(music, Is.Not.Null);
            Assert.That(music.length, Is.GreaterThan(20f).And.GreaterThan(playback.Timeline.Duration()));
            var pending = (System.Collections.ICollection)typeof(GiftScriptHost).GetField("_audioStopCallbacks", Internal).GetValue(playback.Host);
            Assert.That(pending.Count, Is.EqualTo(1));
        }
    }
}
