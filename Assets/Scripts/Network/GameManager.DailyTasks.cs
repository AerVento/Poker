using System.Linq;
using Game.Network.Accounts;

namespace Game.Network
{
    public partial class GameManager
    {
        internal static bool IsTaskRewardPhase(ServerGameInfo info, int playerIndex)
        {
            if (info == null || string.IsNullOrEmpty(info.HandId)) return false;
            if (info.Stage == GameStage.RoundOver || info.Stage == GameStage.GameOver) return true;
            if (playerIndex < 0 || playerIndex >= info.PlayersCount) return false;
            var player = info.Players[playerIndex];
            return player.IsOccupied && player.ParticipatedThisHand && player.IsFold &&
                !player.IsWaitingForNextHand && !player.IsDisconnected && !player.IsLeaving;
        }

        internal TaskRewardAccess GetTaskRewardAccess(int roomId, string accountId)
        {
            if (!_games.TryGetValue(roomId, out var info)) return null;
            var index = info.Players.FindIndex(p => p.IsOccupied && p.AccountId == accountId);
            return IsTaskRewardPhase(info, index) ? new TaskRewardAccess(accountId, roomId,
                IsHandInProgress(roomId) ? info.HandId : null) : null;
        }

        internal void RefreshTaskRewardBalance(int roomId, PlayerAccount account)
        {
            if (!_games.TryGetValue(roomId, out var info)) return;
            var player = info.Players.FirstOrDefault(p => p.IsOccupied && p.AccountId == account.AccountId);
            if (player == null) return;
            player.AccountChips = account.Chips;
            // An in-hand reward does not enter this hand's chips, bets, pot or outcome facts.
            if (!IsHandInProgress(roomId)) player.Chips = account.Chips;
        }
    }
}
