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
    public sealed class TouhouGiftTests
    {
        private const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly string[] Names = { "ResurrectionButterfly", "FantasySeal", "MasterSpark", "TheWorld", "HourOfDaybreak" };
        private static readonly string[] Ids = { "resurrection-butterfly", "fantasy-seal", "master-spark", "the-world", "hour-of-daybreak" };

        [Test]
        public void GiftsHaveResourcesAndNoPurchaseOffer()
        {
            var catalog = CatalogConfiguration.Load();
            for (int i = 0; i < Names.Length; i++)
            {
                Assert.That(catalog.FindItem(Ids[i]).Consumable, Is.True);
                Assert.That(catalog.FindGift(Ids[i]).Effect, Is.EqualTo("gifts/" + Ids[i]));
                Assert.That(catalog.FindOffer(Ids[i]), Is.Null);
                var gift = Load(Names[i]);
                Assert.That(gift.TryDefine(out var script), Is.True, gift.ScriptError);
                foreach (var resource in script.Resources) Assert.That(gift.FindResource(resource), Is.Not.Null, resource);
                Assert.That(script.Duration, Is.InRange(5f, 9f));
            }
        }

        [TestCase("ResurrectionButterfly", -840, -420)]
        [TestCase("ResurrectionButterfly", 840, 420)]
        [TestCase("ResurrectionButterfly", 0, 0)]
        [TestCase("FantasySeal", -840, -420)]
        [TestCase("FantasySeal", 840, 420)]
        [TestCase("FantasySeal", 0, 0)]
        [TestCase("MasterSpark", -840, -420)]
        [TestCase("MasterSpark", 840, 420)]
        [TestCase("MasterSpark", 0, 0)]
        [TestCase("TheWorld", -840, -420)]
        [TestCase("TheWorld", 840, 420)]
        [TestCase("TheWorld", 0, 0)]
        [TestCase("HourOfDaybreak", -840, -420)]
        [TestCase("HourOfDaybreak", 840, 420)]
        [TestCase("HourOfDaybreak", 0, 0)]
        public void TimelineRewindsAtEverySeatAndFadesOut(string name, float x, float y)
        {
            var root = new GameObject("Spell test canvas", typeof(RectTransform), typeof(Canvas));
            Sequence sequence = null;
            GiftScriptHost host = null;
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(1920, 1080);
                var sender = Rect("Sender", rect, new Vector2(-x, -y));
                var target = Rect("Target", rect, new Vector2(x, y));
                var layer = Rect("Visual", rect, Vector2.zero);
                layer.gameObject.AddComponent<CanvasGroup>();
                var gift = Load(name);
                Assert.That(gift.TryDefine(out var script), Is.True, gift.ScriptError);
                host = (GiftScriptHost)Activator.CreateInstance(typeof(GiftScriptHost), Internal, null,
                    new object[] { rect, layer, sender, target, new Func<string, Object>(gift.FindResource), 20241 }, null);
                typeof(GiftScriptRuntime).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { script, host });
                sequence = (Sequence)typeof(GiftScriptHost).GetMethod("Finish", Internal).Invoke(host,
                    new object[] { new Func<bool>(() => true), new Action<AudioClip, float>((clip, volume) => { }), new Action<LuaFunction>(callback => { }) });
                Assert.That(sequence.Duration(), Is.EqualTo(script.Duration).Within(0.001f));
                Assert.That(layer.GetComponentsInChildren<Transform>(true).Length, Is.LessThanOrEqualTo(GiftScriptHost.MaxNodes));
                var graphics = layer.GetComponentsInChildren<Graphic>(true);
                Assert.That(graphics.All(g => !g.raycastTarget), Is.True);
                var actorRoot = layer.GetComponentsInChildren<RectTransform>(true).Single(n => n.name == "Spell character");
                var images = actorRoot.GetComponentsInChildren<Image>(true);
                Assert.That(images.Length, Is.EqualTo(6));
                var face = images.Single(image => image.name == "Character face");
                var arm = images.Single(image => image.name == "Casting arm").rectTransform;
                var parts = images.Select(image => image.sprite).ToArray();
                sequence.Goto(0.5f, false);
                var restAngle = arm.localEulerAngles.z;
                Assert.That(face.sprite, Is.SameAs(gift.FindResource("rigHead")));
                sequence.Goto(name == "TheWorld" ? 5.5f : 4.2f, false);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(restAngle, arm.localEulerAngles.z)), Is.GreaterThan(15), "The shoulder must articulate, not swap an illustration.");
                Assert.That(images.Select(image => image.sprite), Is.EqualTo(parts));
                var nodes = layer.GetComponentsInChildren<RectTransform>(true);
                var positions = nodes.Select(n => n.localPosition).ToArray();
                var scales = nodes.Select(n => n.localScale).ToArray();
                var colors = graphics.Select(g => g.color).ToArray();
                var rotations = nodes.Select(n => n.localEulerAngles.z).ToArray();
                var visible = nodes.Select(n => n.gameObject.activeSelf).ToArray();
                sequence.Goto(0.1f, false);
                sequence.Goto(name == "TheWorld" ? 5.5f : 4.2f, false);
                for (int i = 0; i < nodes.Length; i++)
                {
                    Assert.That(Vector3.Distance(nodes[i].localPosition, positions[i]), Is.LessThan(0.01f), nodes[i].name);
                    Assert.That(Vector3.Distance(nodes[i].localScale, scales[i]), Is.LessThan(0.01f), nodes[i].name);
                    Assert.That(nodes[i].gameObject.activeSelf, Is.EqualTo(visible[i]), nodes[i].name);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(nodes[i].localEulerAngles.z, rotations[i])), Is.LessThan(0.01f), nodes[i].name);
                }
                for (int i = 0; i < graphics.Length; i++) Assert.That(graphics[i].color, Is.EqualTo(colors[i]));
                sequence.Goto(1.6f, false);
                host.ScreenPoint(0.5f, 0.5f, out var centerX, out var centerY);
                var screenCenter = new Vector2(centerX, centerY);
                Assert.That(actorRoot.anchoredPosition, Is.EqualTo(screenCenter), "All characters are staged at screen center.");
                var circle = nodes.Single(n => n.name == "Blue hexagram");
                Assert.That(circle.gameObject.activeSelf, Is.True);
                if (name == "MasterSpark")
                {
                    var beams = nodes.Where(n => n.name.StartsWith("Piercing Master Spark ")).ToArray();
                    Assert.That(beams.Length, Is.EqualTo(5));
                    foreach (var beam in beams)
                        Assert.That(beam.sizeDelta.x, Is.GreaterThan(Vector2.Distance(beam.anchoredPosition, target.anchoredPosition) + 200));
                    Assert.That(beams.Max(n => n.sizeDelta.y), Is.GreaterThan(250));
                }
                if (name == "FantasySeal")
                {
                    var orbs = nodes.Where(n => n.name.StartsWith("Colored spirit orb ")).ToArray();
                    Assert.That(orbs.Length, Is.EqualTo(6));
                    Assert.That(orbs.All(n => n.gameObject.activeSelf), Is.True);
                    Assert.That(orbs.All(n => Vector2.Distance(n.anchoredPosition, screenCenter) > 120f), Is.True, "Orbs are summoned around the caster first.");
                    foreach (var hit in new[] { 3.01f, 3.48f, 3.98f })
                    {
                        sequence.Goto(hit, false);
                        var pair = hit < 3.3f ? 1 : hit < 3.7f ? 2 : 3;
                        foreach (var orb in orbs.Skip((pair - 1) * 2).Take(2))
                            Assert.That(Vector2.Distance(orb.anchoredPosition, target.anchoredPosition), Is.LessThan(0.1f));
                        Assert.That(nodes.Single(n => n.name == "Seal chain explosion " + pair).gameObject.activeSelf, Is.True);
                    }
                    sequence.Goto(3.22f, false);
                    Assert.That(orbs.Skip(2).All(n => Vector2.Distance(n.anchoredPosition, target.anchoredPosition) > 145), Is.True);

                }
                if (name == "ResurrectionButterfly")
                {
                    var butterflies = graphics.Where(n => n.name.StartsWith("Butterfly wave ")).ToArray();
                    Assert.That(butterflies.Length, Is.EqualTo(72));
                    Assert.That(nodes.Count(n => n.name.StartsWith("Fan ripple ")), Is.EqualTo(40));
                    Assert.That(nodes.Count(n => n.name.StartsWith("Delayed ray 0-")), Is.EqualTo(2));
                    Assert.That(nodes.Count(n => n.name.StartsWith("Delayed ray 1-")), Is.EqualTo(3));
                    sequence.Goto(3.2f, false);
                    Assert.That(nodes.Where(n => n.name.StartsWith("Delayed ray ")).All(n => !n.gameObject.activeSelf), Is.True);
                    sequence.Goto(3.4f, false);
                    Assert.That(nodes.Where(n => n.name.StartsWith("Delayed ray 0-")).All(n => n.localScale.x > 0.1f && n.localScale.x < 0.9f), Is.True);
                    var ripple = nodes.Where(n => n.name.StartsWith("Fan ripple 0-0-")).ToArray();
                    sequence.Goto(2f, false);
                    float earlySpread = Vector2.Distance(ripple.First().anchoredPosition, ripple.Last().anchoredPosition);
                    sequence.Goto(3f, false);
                    Assert.That(Vector2.Distance(ripple.First().anchoredPosition, ripple.Last().anchoredPosition), Is.GreaterThan(earlySpread * 2f));
                    foreach (var wave in new[] { 0, 1 })
                        foreach (var layerIndex in new[] { 0, 1, 2 })
                            Assert.That(butterflies.Count(n => n.name.StartsWith("Butterfly wave " + wave + "-" + layerIndex + "-")), Is.EqualTo(12));
                    Assert.That(butterflies.Select(n => new Vector3(n.color.r,n.color.g,n.color.b)).Distinct().Count(), Is.EqualTo(5));
                }
                if (name == "TheWorld")
                {
                    var hand = nodes.Single(n => n.name == "Minute hand");
                    sequence.Goto(0.46f, false); var fastStart = hand.localEulerAngles.z;
                    sequence.Goto(0.50f, false); var fastStep = Mathf.Abs(Mathf.DeltaAngle(fastStart, hand.localEulerAngles.z));
                    sequence.Goto(1.80f, false); var slowStart = hand.localEulerAngles.z;
                    sequence.Goto(1.84f, false); var slowStep = Mathf.Abs(Mathf.DeltaAngle(slowStart, hand.localEulerAngles.z));
                    Assert.That(fastStep, Is.GreaterThan(slowStep * 5f), "The hand must visibly slow down before stopping.");
                    sequence.Goto(2.30f, false); var stoppedHand = hand.localEulerAngles.z;
                    sequence.Goto(2.70f, false);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(stoppedHand, hand.localEulerAngles.z)), Is.LessThan(0.001f));
                    var actor = nodes.Single(n => n.name == "Spell character");
                    var knives = nodes.Where(n => n.name.StartsWith("Target orbit knife ")).ToArray();
                    sequence.Goto(3.7f, false);
                    Assert.That(knives.Length, Is.EqualTo(54));
                    foreach (var knife in knives)
                        Assert.That(Vector2.Distance(knife.anchoredPosition, target.anchoredPosition), Is.InRange(99f, 231f));
                    var screen = nodes.Single(n => n.name == "Frozen screen").GetComponent<RawImage>();
                    Assert.That(screen.material.GetFloat("_Radius"), Is.EqualTo(1).Within(0.001f));
                    Assert.That(screen.material.GetFloat("_Strength"), Is.EqualTo(1).Within(0.001f));
                    sequence.Goto(5.7f, false);
                    Assert.That(screen.material.GetFloat("_Strength"), Is.EqualTo(0).Within(0.001f));
                    sequence.Goto(1f, false);
                    Assert.That(screen.material.GetFloat("_Radius"), Is.EqualTo(0).Within(0.001f));
                    sequence.Goto(1.85f, false);
                    var stopped = images.Select(image => image.transform.localToWorldMatrix).ToArray();
                    sequence.Goto(2.05f, false);
                    Assert.That(images.Select(image => image.transform.localToWorldMatrix), Is.EqualTo(stopped), "Time stop must hold all actor parts still.");
                }
                if (name == "HourOfDaybreak")
                {
                    var stars = nodes.Where(n => n.name.StartsWith("Dawn star ")).ToArray();
                    Assert.That(stars.Length, Is.EqualTo(48));
                    Assert.That(nodes.Count(n => n.name.StartsWith("Dawn ofuda ")), Is.EqualTo(24));
                    Assert.That(nodes.Count(n => n.name.StartsWith("Dawn butterfly ")), Is.EqualTo(24));
                    sequence.Goto(1.9f, false);
                    Assert.That(stars.All(n => n.gameObject.activeSelf), Is.True, "All four rings must be summoned before release.");
                    var held = stars.Select(n => n.anchoredPosition).ToArray();
                    Assert.That(stars.All(n => n.GetComponent<Image>().color.b > n.GetComponent<Image>().color.r), Is.True);
                    sequence.Goto(2.2f, false);
                    Assert.That(stars.Select(n => n.anchoredPosition), Is.EqualTo(held));
                    sequence.Goto(5.6f, false);
                    Assert.That(stars.Select(n => n.anchoredPosition).Distinct().Count(), Is.GreaterThan(40));
                    Assert.That(stars.All(n => n.GetComponent<Image>().color.r > 0.95f && n.GetComponent<Image>().color.g > 0.85f), Is.True);
                    foreach (var star in stars)
                        Assert.That(Vector2.Distance(star.anchoredPosition, target.anchoredPosition), Is.LessThan(142f), "Stars land in a small spread around the target.");
                }
                sequence.Goto(script.Duration, false);
                Assert.That(layer.GetComponent<CanvasGroup>().alpha, Is.LessThan(0.001f));
            }
            finally
            {
                sequence?.Kill();
                if (host != null) typeof(GiftScriptHost).GetMethod("Dispose", Internal).Invoke(host, null);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(-26f, 1f)]
        [TestCase(28f, -1f)]
        [TestCase(0f, 1f)]
        public void JointMeshKeepsShoulderFixedAndHandFollowing(float angle, float direction)
        {
            var type = typeof(GiftScriptHost).Assembly.GetType("Game.UI.Gifts.GiftJointMesh");
            var deform = type.GetMethod("Deform", BindingFlags.Static | BindingFlags.NonPublic);
            var shoulder = new Vector3(0f, 12f, 0f);
            var hand = new Vector3(90f * direction, -10f, 0f);
            var rotation = Quaternion.Euler(0f, 0f, angle);
            var root = (Vector3)deform.Invoke(null, new object[] { shoulder, angle, 40f, direction });
            var tip = (Vector3)deform.Invoke(null, new object[] { hand, angle, 40f, direction });
            Assert.That(Vector3.Distance(rotation * root, shoulder), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(tip, hand), Is.LessThan(0.001f));
            for (int x = 0; x <= 90; x++)
            {
                var point = (Vector3)deform.Invoke(null, new object[] { new Vector3(x * direction, 12f, 0f), angle, 40f, direction });
                Assert.That(float.IsNaN(point.x) || float.IsInfinity(point.x), Is.False);
            }
        }

        [TestCase(SpellBulletGraphic.Shape.Butterfly)]
        [TestCase(SpellBulletGraphic.Shape.Knife)]
        [TestCase(SpellBulletGraphic.Shape.Star)]
        [TestCase(SpellBulletGraphic.Shape.Beam)]
        [TestCase(SpellBulletGraphic.Shape.Cannon)]
        [TestCase(SpellBulletGraphic.Shape.Orb)]
        public void BulletMeshStaysFiniteAndInsideItsRect(SpellBulletGraphic.Shape kind)
        {
            var go = new GameObject("Bullet", typeof(RectTransform));
            try
            {
                var graphic = go.AddComponent<SpellBulletGraphic>();
                graphic.Kind = kind;
                graphic.rectTransform.sizeDelta = new Vector2(100, 80);
                using (var vertices = new VertexHelper())
                {
                    typeof(SpellBulletGraphic).GetMethod("OnPopulateMesh", Internal, null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { vertices });
                    Assert.That(vertices.currentVertCount, Is.GreaterThan(2));
                    var vertex = new UIVertex();
                    for (int i = 0; i < vertices.currentVertCount; i++)
                    {
                        vertices.PopulateUIVertex(ref vertex, i);
                        Assert.That(vertex.position.x, Is.InRange(-50f, 50f));
                        Assert.That(vertex.position.y, Is.InRange(-40f, 40f));
                    }
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static GiftEffectSO Load(string name)
        {
            var gift = AssetDatabase.LoadAssetAtPath<GiftEffectSO>("Assets/RemoteContent/gifts/" + name + ".asset");
            gift.ReloadFromDisk();
            return gift;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(260, 110);
            rect.anchoredPosition = position;
            return rect;
        }
    }
}
