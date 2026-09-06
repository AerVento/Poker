using Game.Network;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    // Public, server-stamped actions only. Initial/recovery snapshots establish a silent baseline.
    public sealed class PokerOperationAudioState
    {
        private int _roomId = -1;
        private string _handId;
        private ulong _snapshotTurn;
        private ulong _lastAction;
        private readonly Dictionary<ulong, string> _localActions = new Dictionary<ulong, string>();

        public void Reset()
        {
            _roomId = -1;
            _handId = null;
            _snapshotTurn = 0;
            _lastAction = 0;
            _localActions.Clear();
        }

        public void RecordLocal(ClientGameInfo info)
        {
            if (info.RoomInfo?.Id == _roomId && info.HandId == _handId && !info.IsSpectator)
                _localActions[info.TurnId] = info.CurrentPlayer.PlayerName;
        }

        public List<SoundCue> Observe(ClientGameInfo info)
        {
            var cues = new List<SoundCue>();
            if (info.RoomInfo == null || string.IsNullOrEmpty(info.HandId) || info.Players == null)
                return cues;
            bool baseline = _roomId != info.RoomInfo.Id || _handId != info.HandId;
            if (baseline)
            {
                Reset();
                _roomId = info.RoomInfo.Id;
                _handId = info.HandId;
            }
            else if (info.TurnId < _snapshotTurn)
                return cues;

            foreach (var player in info.Players.Where(player => player.IsOccupied &&
                !player.IsWaitingForNextHand && player.LastOperation != null)
                .OrderBy(player => player.LastOperation.TurnId))
            {
                var operation = player.LastOperation;
                if (operation.TurnId == 0 || operation.TurnId <= _lastAction)
                    continue;
                _lastAction = operation.TurnId;
                bool echoed = _localActions.TryGetValue(operation.TurnId, out var playerName) &&
                    playerName == player.PlayerName;
                if (!baseline && !echoed)
                {
                    var cue = GetCue(operation.Type, player.IsAllIn);
                    if (cue.HasValue)
                        cues.Add(cue.Value);
                }
            }
            _snapshotTurn = info.TurnId;
            foreach (var turn in _localActions.Keys.Where(turn => turn < _snapshotTurn).ToArray())
                _localActions.Remove(turn);
            return cues;
        }

        private static SoundCue? GetCue(PlayerOperationType type, bool isAllIn)
        {
            if (isAllIn && (type == PlayerOperationType.Bet || type == PlayerOperationType.Raise ||
                type == PlayerOperationType.Call || type == PlayerOperationType.AllIn))
                return SoundCue.AllIn;
            return type switch
            {
                PlayerOperationType.Check => SoundCue.Check,
                PlayerOperationType.Bet => SoundCue.Bet,
                PlayerOperationType.Raise => SoundCue.Raise,
                PlayerOperationType.Call => SoundCue.Call,
                PlayerOperationType.AllIn => SoundCue.AllIn,
                PlayerOperationType.Fold => SoundCue.Fold,
                _ => null,
            };
        }
    }
}
