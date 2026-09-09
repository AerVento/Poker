using Game.Network;
using Game.UI;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public class AudioTest
    {
        [TestCase(PlayerOperationType.Check, SoundCue.Check)]
        [TestCase(PlayerOperationType.Bet, SoundCue.Bet)]
        [TestCase(PlayerOperationType.Raise, SoundCue.Raise)]
        [TestCase(PlayerOperationType.Call, SoundCue.Call)]
        [TestCase(PlayerOperationType.AllIn, SoundCue.AllIn)]
        [TestCase(PlayerOperationType.Fold, SoundCue.Fold)]
        public void RemoteOperations_PlayOnceForEachAuthoritativeAction(PlayerOperationType type, SoundCue cue)
        {
            var state = new PokerOperationAudioState();
            var info = Info();
            Assert.IsEmpty(state.Observe(info));
            var other = info.Players[1];
            other.LastOperation = new PlayerOperation { Type = type, TurnId = 1 };
            info.Players[1] = other;
            info.TurnId = 2;
            CollectionAssert.AreEqual(new[] { cue }, state.Observe(info));
            Assert.IsEmpty(state.Observe(info));
            other.LastOperation = new PlayerOperation { Type = type, TurnId = 3 };
            info.Players[1] = other;
            info.TurnId = 4;
            CollectionAssert.AreEqual(new[] { cue }, state.Observe(info));
        }

        [Test]
        public void RemoteOperations_LocalEchoIsSilentButOwnAutomaticActionAndSpectatorAudioRemainAudible()
        {
            var state = new PokerOperationAudioState();
            var info = Info();
            state.Observe(info);
            state.RecordLocal(info);
            var player = info.Players[0];
            player.LastOperation = new PlayerOperation { Type = PlayerOperationType.Check, TurnId = 1 };
            info.Players[0] = player;
            info.TurnId = 2;
            Assert.IsEmpty(state.Observe(info));
            player.LastOperation = new PlayerOperation { Type = PlayerOperationType.Fold, TurnId = 3 };
            info.Players[0] = player;
            info.TurnId = 4;
            CollectionAssert.AreEqual(new[] { SoundCue.Fold }, state.Observe(info));
            info.CurrentPlayerIndex = -1;
            player.IsAllIn = true;
            player.LastOperation = new PlayerOperation { Type = PlayerOperationType.Call, TurnId = 5 };
            info.Players[0] = player;
            info.TurnId = 6;
            CollectionAssert.AreEqual(new[] { SoundCue.AllIn }, state.Observe(info));
        }

        [Test]
        public void RemoteOperations_ReconnectStreetResetAndDuplicateSnapshotsDoNotReplayHistory()
        {
            var state = new PokerOperationAudioState();
            var info = Info(turn: 8);
            var player = info.Players[1];
            player.LastOperation = new PlayerOperation { Type = PlayerOperationType.Raise, TurnId = 7 };
            info.Players[1] = player;
            Assert.IsEmpty(state.Observe(info));
            info.Round = GameStage.Flop;
            info.TurnId = 9;
            player.LastOperation = null;
            info.Players[1] = player;
            Assert.IsEmpty(state.Observe(info));
            player.LastOperation = new PlayerOperation { Type = PlayerOperationType.Check, TurnId = 9 };
            info.Players[1] = player;
            info.TurnId = 10;
            CollectionAssert.AreEqual(new[] { SoundCue.Check }, state.Observe(info));
            info.TurnId = 8;
            Assert.IsEmpty(state.Observe(info));
            state.Reset();
            Assert.IsEmpty(state.Observe(info));
        }

        [Test]
        public void ActionIdentity_SurvivesClientProjectionAndJsonSync()
        {
            var info = Info(turn: 12);
            var player = info.Players[1];
            player.LastOperation = new PlayerOperation { Type = PlayerOperationType.Check, TurnId = 11 };
            info.Players[1] = player;
            var message = new GameInfoSync { Info = info };
            Assert.AreEqual(11ul, message.Info.Players[1].LastOperation.TurnId);
        }

        private static ClientGameInfo Info(string hand = "hand-a", ulong turn = 1)
        {
            return new ClientGameInfo
            {
                RoomInfo = new RoomInfo { Id = 12 },
                HandId = hand,
                TurnId = turn,
                Round = GameStage.Preflop,
                CurrentPlayerIndex = 0,
                OperatingPlayer = 0,
                Players = new List<ClientGameInfo.PlayerInfo>
                {
                    new ClientGameInfo.PlayerInfo { PlayerName = "Alice", IsOccupied = true, Chips = 1000 },
                    new ClientGameInfo.PlayerInfo { PlayerName = "Bob", IsOccupied = true, Chips = 1000 },
                },
            };
        }

        [Test]
        public void TurnReminder_OnlyOncePerActionOpportunity()
        {
            var state = new PokerAudioState();
            var info = Info();
            Assert.IsTrue(state.Observe(info));
            Assert.IsFalse(state.Observe(info));
            info.TotalPot = 200;
            Assert.IsFalse(state.Observe(info));
            info.TurnId = 3;
            Assert.IsTrue(state.Observe(info));
            info.TurnId = 2;
            Assert.IsFalse(state.Observe(info), "A delayed update must not replay an old turn.");
            Assert.IsTrue(state.Observe(Info("hand-b")));
        }

        [TestCase("spectator")]
        [TestCase("waiting")]
        [TestCase("fold")]
        [TestCase("allin")]
        [TestCase("disconnect")]
        [TestCase("leaving")]
        [TestCase("vacant")]
        [TestCase("other-turn")]
        [TestCase("round-over")]
        public void TurnReminder_IneligiblePlayersAreSilent(string status)
        {
            var info = Info();
            var player = info.Players[0];
            switch (status)
            {
                case "spectator": info.CurrentPlayerIndex = -1; break;
                case "waiting": player.IsWaitingForNextHand = true; break;
                case "fold": player.IsFold = true; break;
                case "allin": player.IsAllIn = true; break;
                case "disconnect": player.IsDisconnected = true; break;
                case "leaving": player.IsLeaving = true; break;
                case "vacant": player.IsOccupied = false; break;
                case "other-turn": info.OperatingPlayer = 1; break;
                case "round-over": info.Round = GameStage.RoundOver; break;
            }
            info.Players[0] = player;
            Assert.IsFalse(new PokerAudioState().Observe(info));
        }

        [Test]
        public void AllInMusic_PersistsAcrossStreetsAndResultsUntilNewHand()
        {
            var state = new PokerAudioState();
            var info = Info();
            state.Observe(info);
            Assert.AreEqual(MusicCue.Table, state.Music);
            var other = info.Players[1];
            other.IsAllIn = true;
            info.Players[1] = other;
            state.Observe(info);
            Assert.AreEqual(MusicCue.AllIn, state.Music);
            other.IsAllIn = false;
            info.Players[1] = other;
            info.Round = GameStage.Flop;
            state.Observe(info);
            Assert.AreEqual(MusicCue.AllIn, state.Music);
            info.Round = GameStage.RoundOver;
            state.Observe(info);
            Assert.AreEqual(MusicCue.AllIn, state.Music);
            state.Observe(Info("hand-b"));
            Assert.AreEqual(MusicCue.Table, state.Music);
            state.EnterLobby();
            Assert.AreEqual(MusicCue.Lobby, state.Music);
        }

        [Test]
        public void AllInMusic_SpectatorsAndReconnectSnapshotsStillHearIt()
        {
            var info = Info();
            info.CurrentPlayerIndex = -1;
            var other = info.Players[1];
            other.IsAllIn = true;
            info.Players[1] = other;
            var state = new PokerAudioState();
            Assert.IsFalse(state.Observe(info));
            Assert.AreEqual(MusicCue.AllIn, state.Music);
            state.EnterLobby();
            Assert.IsFalse(state.Observe(info));
            Assert.AreEqual(MusicCue.AllIn, state.Music);
        }

        [TestCase(0u, SoundCue.Lose)]
        [TestCase(1u, SoundCue.Win)]
        [TestCase(200u, SoundCue.Win)]
        public void Result_AnyPotAwardWinsAndOnlyPlaysOnce(uint earned, SoundCue expected)
        {
            var state = new PokerAudioState();
            state.Observe(Info());
            var result = new RoundResult { PlayerResults = new List<RoundResult.Player>
            {
                new RoundResult.Player { PlayerName = "Alice", ChipEarned = earned },
            } };
            Assert.IsNull(state.GetResult(99, "hand-a", "Alice", result));
            Assert.IsNull(state.GetResult(12, "old-hand", "Alice", result));
            Assert.IsNull(state.GetResult(12, "hand-a", "Spectator", result));
            Assert.AreEqual(expected, state.GetResult(12, "hand-a", "Alice", result));
            Assert.IsNull(state.GetResult(12, "hand-a", "Alice", result));
            Assert.IsFalse(state.Observe(Info("hand-a", 2)));
            state.Observe(Info("hand-b"));
            Assert.AreEqual(expected, state.GetResult(12, "hand-b", "Alice", result));
        }

        [TestCase(PlayerOperationType.Check, 0u, 0u, SoundCue.Check)]
        [TestCase(PlayerOperationType.Bet, 0u, 100u, SoundCue.Bet)]
        [TestCase(PlayerOperationType.Bet, 100u, 200u, SoundCue.Raise)]
        [TestCase(PlayerOperationType.Call, 100u, 0u, SoundCue.Call)]
        [TestCase(PlayerOperationType.Call, 1000u, 0u, SoundCue.AllIn)]
        [TestCase(PlayerOperationType.Bet, 100u, 1000u, SoundCue.AllIn)]
        [TestCase(PlayerOperationType.AllIn, 100u, 0u, SoundCue.AllIn)]
        [TestCase(PlayerOperationType.Fold, 100u, 0u, SoundCue.Fold)]
        public void OperationSound_DistinguishesRaisesAndImplicitAllIns(
            PlayerOperationType type, uint highestBet, uint amount, SoundCue expected)
        {
            var info = Info();
            info.CurrentHighestPlayerBet = highestBet;
            Assert.AreEqual(expected, PokerAudioState.GetOperation(type, info, amount));
        }

        [Test]
        public void AudioAssets_AllCuesAndSettingsControlsAreAssigned()
        {
            var library = Resources.Load<GameAudioLibrary>(GameAudioManager.LibraryResourcePath);
            Assert.NotNull(library);
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
                Assert.NotNull(library.GetSound(cue), cue.ToString());
            foreach (var cue in new[] { MusicCue.Lobby, MusicCue.Table, MusicCue.AllIn })
            {
                var clip = library.GetMusic(cue);
                Assert.NotNull(clip);
                Assert.That(clip.length, Is.InRange(40f, 70f));
                StringAssert.Contains("_Loop_", AssetDatabase.GetAssetPath(clip));
            }
            var options = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/OptionPanel/OptionPanel.prefab");
            var serialized = new SerializedObject(options.GetComponent<OptionPanel>());
            foreach (var field in new[] { "_musicVolume", "_soundVolume" })
            {
                var slider = serialized.FindProperty(field).objectReferenceValue as Slider;
                Assert.NotNull(slider, field);
                Assert.AreEqual(0, slider.minValue);
                Assert.AreEqual(1, slider.maxValue);
                Assert.NotNull(slider.handleRect);
                Assert.NotNull(slider.fillRect);
            }
            foreach (var field in new[] { "_musicVolumeLabel", "_soundVolumeLabel", "_audioCredits" })
                Assert.NotNull(serialized.FindProperty(field).objectReferenceValue, field);
            var game = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/GamePanel/GamePanel.prefab");
            Assert.NotNull(new SerializedObject(game.GetComponent<GamePanel>()).FindProperty("_settingsButton").objectReferenceValue);
        }

        [Test]
        public void VolumePreferences_AreIndependentClampedAndReloadable()
        {
            bool hadMusic = PlayerPrefs.HasKey(AudioPreferences.MusicVolumeKey);
            bool hadSound = PlayerPrefs.HasKey(AudioPreferences.SoundVolumeKey);
            float oldMusic = PlayerPrefs.GetFloat(AudioPreferences.MusicVolumeKey);
            float oldSound = PlayerPrefs.GetFloat(AudioPreferences.SoundVolumeKey);
            try
            {
                AudioPreferences.SetMusicVolume(0.23f);
                AudioPreferences.SetSoundVolume(0.61f);
                AudioPreferences.Save();
                Assert.AreEqual(0.23f, AudioPreferences.MusicVolume, 0.001f);
                Assert.AreEqual(0.61f, AudioPreferences.SoundVolume, 0.001f);
                AudioPreferences.SetMusicVolume(0);
                Assert.AreEqual(0, AudioPreferences.MusicVolume);
                Assert.AreEqual(0.61f, AudioPreferences.SoundVolume, 0.001f);
                AudioPreferences.SetMusicVolume(2);
                AudioPreferences.SetSoundVolume(-1);
                Assert.AreEqual(1, AudioPreferences.MusicVolume);
                Assert.AreEqual(0, AudioPreferences.SoundVolume);
                Assert.AreEqual(0, AudioPreferences.Sanitize(float.NaN));
            }
            finally
            {
                if (hadMusic) PlayerPrefs.SetFloat(AudioPreferences.MusicVolumeKey, oldMusic);
                else PlayerPrefs.DeleteKey(AudioPreferences.MusicVolumeKey);
                if (hadSound) PlayerPrefs.SetFloat(AudioPreferences.SoundVolumeKey, oldSound);
                else PlayerPrefs.DeleteKey(AudioPreferences.SoundVolumeKey);
                PlayerPrefs.Save();
            }
        }
    }
}
