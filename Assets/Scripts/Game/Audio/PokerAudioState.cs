using Game.Network;
using System;

namespace Game
{
    // Tracks audible transitions independently of panel visibility and refreshes.
    public sealed class PokerAudioState
    {
        private int _roomId = -1;
        private string _handId;
        private ulong _lastNotifiedTurn;
        private bool _hasNotifiedTurn;
        private bool _resultPlayed;

        public MusicCue Music { get; private set; } = MusicCue.Lobby;

        public void EnterLobby()
        {
            _roomId = -1;
            _handId = null;
            _hasNotifiedTurn = false;
            _resultPlayed = false;
            Music = MusicCue.Lobby;
        }

        public bool Observe(ClientGameInfo info)
        {
            if (info.RoomInfo == null || string.IsNullOrEmpty(info.HandId))
                return false;

            if (_roomId != info.RoomInfo.Id || _handId != info.HandId)
            {
                _roomId = info.RoomInfo.Id;
                _handId = info.HandId;
                _hasNotifiedTurn = false;
                _resultPlayed = false;
                Music = MusicCue.Table;
            }

            if (info.Players != null)
            {
                foreach (var player in info.Players)
                {
                    if (player.IsOccupied && !player.IsWaitingForNextHand && player.IsAllIn)
                    {
                        Music = MusicCue.AllIn;
                        break;
                    }
                }
            }

            if (!CanOperate(info) || _resultPlayed ||
                _hasNotifiedTurn && info.TurnId <= _lastNotifiedTurn)
                return false;

            _lastNotifiedTurn = info.TurnId;
            _hasNotifiedTurn = true;
            return true;
        }

        public SoundCue? GetResult(int roomId, string handId, string playerName, RoundResult result)
        {
            if (_resultPlayed || _roomId != roomId || string.IsNullOrEmpty(_handId) ||
                _handId != handId || string.IsNullOrEmpty(playerName) || result?.PlayerResults == null)
                return null;

            foreach (var player in result.PlayerResults)
            {
                if (player.PlayerName != playerName)
                    continue;
                _resultPlayed = true;
                return player.ChipEarned > 0 ? SoundCue.Win : SoundCue.Lose;
            }
            return null;
        }

        public static bool CanOperate(ClientGameInfo info)
        {
            if (info.RoomInfo == null || info.IsSpectator || info.Round >= GameStage.RoundOver ||
                info.OperatingPlayer != info.CurrentPlayerIndex)
                return false;
            var player = info.CurrentPlayer;
            return player.IsOccupied && !player.IsFold && !player.IsAllIn && !player.IsDisconnected &&
                !player.IsLeaving && !player.IsWaitingForNextHand;
        }

        public static SoundCue GetOperation(PlayerOperationType type, ClientGameInfo info, uint number)
        {
            var player = info.CurrentPlayer;
            bool usesAllChips = player.Chips > 0 &&
                (type == PlayerOperationType.AllIn ||
                 type == PlayerOperationType.Call && info.CurrentHighestPlayerBet > player.StageBet &&
                    (ulong)info.CurrentHighestPlayerBet - player.StageBet >= player.Chips ||
                 (type == PlayerOperationType.Bet || type == PlayerOperationType.Raise) &&
                    (ulong)number >= (ulong)player.StageBet + player.Chips);
            if (usesAllChips)
                return SoundCue.AllIn;

            return type switch
            {
                PlayerOperationType.Check => SoundCue.Check,
                PlayerOperationType.Bet => info.CurrentHighestPlayerBet == 0 ? SoundCue.Bet : SoundCue.Raise,
                PlayerOperationType.Raise => SoundCue.Raise,
                PlayerOperationType.Call => SoundCue.Call,
                PlayerOperationType.AllIn => SoundCue.AllIn,
                PlayerOperationType.Fold => SoundCue.Fold,
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
        }
    }
}
