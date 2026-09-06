using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Game.Network.Diagnostics
{
    public sealed partial class PokerIntegrationRunner
    {
        private GameAudioManager _observedAudio;
        private string _audioHand;
        private ulong _audioTurn;
        private int _remoteSoundsVerified;
        private readonly List<SoundCue> _recentSounds = new List<SoundCue>();
        private readonly Dictionary<SoundCue, int> _soundCounts = new Dictionary<SoundCue, int>();

        private void ObserveAudioRequests()
        {
            _audioHand = null;
            _recentSounds.Clear();
            var audio = GameAudioManager.EnsureClient();
            if (_observedAudio == audio)
                return;
            if (_observedAudio != null)
                _observedAudio.SoundRequested -= RecordSound;
            _observedAudio = audio;
            if (audio != null)
                audio.SoundRequested += RecordSound;
        }

        private void RecordSound(SoundCue cue)
        {
            _recentSounds.Add(cue);
            _soundCounts.TryGetValue(cue, out var count);
            _soundCounts[cue] = count + 1;
        }

        private void VerifyRemoteAudio(ClientGameInfo info)
        {
            foreach (var player in info.Players.Where(player => player.IsOccupied &&
                !player.IsWaitingForNextHand && player.LastOperation != null)
                .OrderBy(player => player.LastOperation.TurnId))
            {
                var operation = player.LastOperation;
                if (operation.TurnId == 0)
                {
                    Fail("Confirmed action has no authoritative turn identity.");
                    return;
                }
                if (info.HandId != _audioHand || operation.TurnId <= _audioTurn || player.PlayerName == _userName)
                    continue;
                var cue = player.IsAllIn ? SoundCue.AllIn : operation.Type switch
                {
                    PlayerOperationType.Check => SoundCue.Check,
                    PlayerOperationType.Bet => SoundCue.Bet,
                    PlayerOperationType.Raise => SoundCue.Raise,
                    PlayerOperationType.Call => SoundCue.Call,
                    PlayerOperationType.AllIn => SoundCue.AllIn,
                    _ => SoundCue.Fold,
                };
                if (!_recentSounds.Remove(cue))
                {
                    Fail("Remote action did not request its sound: " + operation.Type);
                    return;
                }
                _remoteSoundsVerified++;
            }
            _audioHand = info.HandId;
            _audioTurn = info.TurnId > 0 ? info.TurnId - 1 : 0;
            _recentSounds.Clear();
            WriteResult("client-" + _clientIndex + ".audio.json", JsonSerializer.Serialize(new
            {
                RemoteSoundsVerified = _remoteSoundsVerified,
                SoundRequests = _soundCounts.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            }));
        }
    }
}
