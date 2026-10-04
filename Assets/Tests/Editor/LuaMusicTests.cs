using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DG.Tweening;
using Game.UI.Gifts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XLua;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class LuaMusicTests
    {
        private const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class ReleaseCounter : IDisposable
        {
            public int Released;
            public void Dispose() => Released++;
        }

        private sealed class Playback : IDisposable
        {
            public readonly GameObject Root;
            public readonly GiftScriptHost Host;
            public readonly Sequence Timeline;
            public readonly ReleaseCounter Resource = new ReleaseCounter();
            public int Retained;
            private readonly GiftScript _script;

            public Playback(AudioClip clip, string track = "same", bool preview = false, string body = null, Func<IDisposable> retain = null)
            {
                Root = new GameObject("Lua music test", typeof(RectTransform), typeof(Canvas));
                var rect = (RectTransform)Root.transform;
                rect.sizeDelta = new Vector2(1920, 1080);
                var layer = new GameObject("Layer", typeof(RectTransform)).GetComponent<RectTransform>();
                layer.SetParent(rect, false);
                Host = (GiftScriptHost)Activator.CreateInstance(typeof(GiftScriptHost), Internal, null,
                    new object[] { rect, layer, rect, rect, new Func<string, Object>(name => clip), 1 }, null);
                typeof(GiftScriptHost).GetProperty("AudioEnabled", Internal).SetValue(Host, new Func<bool>(() => true));
                typeof(GiftScriptHost).GetProperty("RetainAudioResources", Internal).SetValue(Host,
                    new Func<IDisposable>(() => { Retained++;return retain != null ? retain() : Resource; }));
                var sources = new Dictionary<string, string>
                {
                    ["TouhouMusic"] = File.ReadAllText("Assets/Scripts/UI/Gifts/Lua/Shared/TouhouMusic.lua.txt"),
                    ["Entry"] = "local Music=require('TouhouMusic')\nreturn {apiVersion=1,duration=1,resources={'theme'},build=function(g)\n" +
                        (body ?? "Music.play(g,'" + track + "',0.05,'theme',0.8)") + "\ng.callback(1,function() end)\nend}"
                };
                // Each call deliberately has a separate require cache, as different gift definitions do.
                _script = GiftScriptRuntime.Define("Entry", sources);
                typeof(GiftScriptRuntime).GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { _script, Host });
                Timeline = (Sequence)typeof(GiftScriptHost).GetMethod("Finish", Internal).Invoke(Host,
                    new object[] { new Func<bool>(() => preview), new Action<AudioClip, float>((c, v) => { }),
                        new Action<LuaFunction>(function => typeof(GiftScriptRuntime).GetMethod("Call", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { function })) });
            }

            public void Start() { Timeline.SetUpdate(UpdateType.Manual, true).Play(); DOTween.ManualUpdate(.2f, .2f); }
            public void StopAudio() => typeof(GiftScriptHost).GetMethod("EndAudio", Internal).Invoke(Host, null);
            public void Dispose()
            {
                Timeline.Kill();
                typeof(GiftScriptHost).GetMethod("Dispose", Internal).Invoke(Host, null);
                var module = (LuaTable)typeof(GiftScript).GetProperty("Module", Internal).GetValue(_script);
                module.Dispose();
                Object.DestroyImmediate(Root);
            }
        }

        private AudioClip _clip;
        private AudioClip _other;
        private static GiftMusicPlayback[] Sources => Object.FindObjectsOfType<GiftMusicPlayback>();
        private static void Advance(GiftMusicPlayback source, float seconds) =>
            typeof(GiftMusicPlayback).GetMethod("Advance", Internal).Invoke(source, new object[] { seconds });

        [SetUp]
        public void SetUp()
        {
            _clip = AudioClip.Create("Phrase", 88200, 1, 44100, false);
            _other = AudioClip.Create("Other phrase", 88200, 1, 44100, false);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var source in Sources)
                typeof(GiftMusicPlayback).GetMethod("Stop", Internal).Invoke(source, null);
            Object.DestroyImmediate(_clip);Object.DestroyImmediate(_other);
        }

        [Test]
        public void DifferentDefinitionEnvironmentsShareSameMusicAndKeepFirstBundle()
        {
            using var first = new Playback(_clip);
            using var second = new Playback(_clip);
            first.Start();var source = Sources[0];Advance(source, .3f);
            second.Start();
            Assert.That(Sources, Has.Length.EqualTo(1));
            Assert.That(Sources[0], Is.SameAs(source));
            Assert.That(first.Retained, Is.EqualTo(1));Assert.That(second.Retained, Is.Zero);
            Assert.That(source.GetComponent<AudioSource>().loop, Is.True);
            first.StopAudio();first.StopAudio();Advance(source, 1f);
            Assert.That(source != null, Is.True);Assert.That(first.Resource.Released, Is.Zero);
            second.StopAudio();Advance(source, .1f);
            Assert.That(first.Resource.Released, Is.Zero);
            Advance(source, .21f);
            Assert.That(Sources, Is.Empty);Assert.That(first.Resource.Released, Is.EqualTo(1));
        }

        [Test]
        public void DifferentTrackIsSkippedWithoutQueueOrResourceRetention()
        {
            using var first = new Playback(_clip);
            using var other = new Playback(_other, "other");
            first.Start();var source = Sources[0];Advance(source, .3f);
            other.Start();Assert.That(other.Retained, Is.Zero);
            Assert.That(source.GetComponent<AudioSource>().clip, Is.SameAs(_clip));
            first.StopAudio();Advance(source, .31f);
            Assert.That(Sources, Is.Empty);
            other.StopAudio();Assert.That(Sources, Is.Empty);
            using var later = new Playback(_other, "other");
            later.Start();Assert.That(Sources[0].GetComponent<AudioSource>().clip, Is.SameAs(_other));
        }

        [Test]
        public void MatchingGiftResumesFadeButDifferentGiftCannotTakeOver()
        {
            using var first = new Playback(_clip);
            first.Start();var source = Sources[0];Advance(source, .3f);
            first.StopAudio();Advance(source, .1f);
            using var other = new Playback(_other, "other");other.Start();Assert.That(other.Retained, Is.Zero);
            using var again = new Playback(_clip);again.Start();Advance(source, .4f);
            Assert.That(Sources[0], Is.SameAs(source));Assert.That(first.Resource.Released, Is.Zero);
            again.StopAudio();Advance(source, .31f);Assert.That(Sources, Is.Empty);
        }

        [Test]
        public void PreviewAndCancellationBeforeStartCreateNoMusic()
        {
            using var preview = new Playback(_clip, preview: true);preview.Start();
            using var cancelled = new Playback(_clip);cancelled.StopAudio();cancelled.Start();
            Assert.That(Sources, Is.Empty);Assert.That(preview.Retained + cancelled.Retained, Is.Zero);
        }

        [Test]
        public void GenericCleanupFailureCannotLeaveAnOrphanedLoop()
        {
            using var playback = new Playback(_clip, body:
                "g.onAudioStop(function() error('cleanup test failure') end)\n" +
                "g.callback(0.05,function() g.musicStart('theme',0.8,true) end)");
            playback.Start();Assert.That(Sources, Has.Length.EqualTo(1));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Gift cleanup failed:.*cleanup test failure"));
            playback.StopAudio();
            Assert.That(Sources, Is.Empty);Assert.That(playback.Resource.Released, Is.EqualTo(1));
        }

        [Test]
        public void StopCallbacksRunExactlyOnceAndRemainSeparateFromAudioStop()
        {
            using var playback = new Playback(_clip, body:
                "local calls=0\ng.onStop(function() calls=calls+1; assert(calls==1); end)\n" +
                "Music.play(g,'same',0.05,'theme',0.8)");
            playback.Start();var source = Sources[0];Advance(source, .3f);
            playback.StopAudio();playback.StopAudio();Advance(source,.31f);
            typeof(GiftScriptHost).GetMethod("Dispose", Internal).Invoke(playback.Host, null);
            typeof(GiftScriptHost).GetMethod("Dispose", Internal).Invoke(playback.Host, null);
            Assert.That(playback.Resource.Released, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LuaSharedStateSurvivesDefinitionReloadAndExpiredHandlesReset()
        {
            using (var first = new Playback(_clip))
            {
                first.Start();var source = Sources[0];Advance(source,.3f);first.StopAudio();Advance(source,.31f);
            }
            using var reloaded = new Playback(_other,"new");reloaded.Start();
            Assert.That(Sources[0].GetComponent<AudioSource>().clip, Is.SameAs(_other));
        }
    }
}
