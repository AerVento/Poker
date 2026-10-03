using System.Collections.Generic;
using Game.Network.Accounts;
using Game.UI.Gifts;
using Mirror;

namespace Game.Network
{
    // Giving is the Give consume method of ItemConsumeRequest; this file holds its table rules and the playback broadcast.
    public partial class NetworkRoom
    {
        private readonly Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)> _giftRequests =
            new Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)>();

        // Only seated players at a started table give gifts, and only to other seated players.
        private static ItemActionResult ValidateGive(IReadOnlyList<ItemConsumeCommand> commands, RoomInfo room, RoomSeatInfo senderSeat)
        {
            if (room == null || room.IsWaiting) return ItemActionResult.RoomStartedRequired;
            if (senderSeat == null || senderSeat.State == RoomSeatState.Leaving) return ItemActionResult.SenderNotSeated;
            foreach (var command in commands)
            {
                var target = room.FindSeat(command.TargetSeatIndex);
                if (target == null || target.State == RoomSeatState.Leaving) return ItemActionResult.TargetNotFound;
                if (command.TargetSeatIndex == senderSeat.SeatIndex) return ItemActionResult.CannotTargetSelf;
            }
            return ItemActionResult.Success;
        }

        private void RegisterGiftClientHandlers()
        {
            NetworkClient.RegisterHandler<GiftPlaybackBatchMessage>(message =>
                GiftInteractionController.EnsureActive()?.ReceivePlayback(message));
        }

        private void ClearGiftServer() => _giftRequests.Clear();

        private void ClearGiftClient() => GiftInteractionController.Active?.ResetSession();
    }
}
