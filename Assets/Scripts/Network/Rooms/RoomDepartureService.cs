using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Network
{
    /// <summary>
    /// Defers active-hand requests; the caller applies queued departures after result viewing.
    /// The caller supplies the authenticated player name, never a request's claimed name.
    /// </summary>
    public sealed class RoomDepartureService
    {
        private readonly RoomManager _rooms;
        private readonly GameManager _games;
        private readonly Func<string, bool> _clearLastRoom;

        public RoomDepartureService(RoomManager rooms, GameManager games, Func<string, bool> clearLastRoom)
        {
            _rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            _games = games ?? throw new ArgumentNullException(nameof(games));
            _clearLastRoom = clearLastRoom ?? throw new ArgumentNullException(nameof(clearLastRoom));
        }

        public RoomLeaveStatus RequestForHand(int roomId, string playerName, string handId,
            bool requireImmediate, bool cancelPending = false)
        {
            var game = _games.GetGameInfo(roomId);
            if ((handId ?? string.Empty) != (game?.HandId ?? string.Empty) ||
                (requireImmediate && _games.RequiresDeferredLeave(roomId, playerName)))
                return RoomLeaveStatus.StaleRequest;
            var seat = _rooms.FindRoom(roomId)?.FindSeat(playerName);
            if (!requireImmediate && !cancelPending && game?.Stage == GameStage.RoundOver && seat != null)
            {
                // Preserve a reservation sent before settlement even if it arrives during result viewing.
                seat.LeaveAfterHand = true;
                return RoomLeaveStatus.Queued;
            }
            return Request(roomId, playerName, cancelPending);
        }

        public RoomLeaveStatus Request(int roomId, string playerName, bool cancelPending = false)
        {
            var room = _rooms.FindRoom(roomId);
            if (room == null)
                return RoomLeaveStatus.RoomNotFound;
            if (!room.ContainsMember(playerName))
                return RoomLeaveStatus.NotMember;

            var seat = room.FindSeat(playerName);
            if (cancelPending)
            {
                var reservedSeats = _games.GetGameInfo(roomId)?.Stage == GameStage.RoundOver
                    ? _games.CountEligiblePlayers(roomId)
                    : room.Seats.Count(candidate => !candidate.LeaveAfterHand && !candidate.SitOutAfterHand);
                if (seat != null && seat.LeaveAfterHand && !seat.SitOutAfterHand &&
                    reservedSeats + room.NextHandPlayers.Count >= room.MaxPlayerCount)
                    return RoomLeaveStatus.TableFull;
                if (seat != null)
                    seat.LeaveAfterHand = false;
                return RoomLeaveStatus.Cancelled;
            }

            if (_games.RequiresDeferredLeave(roomId, playerName))
            {
                seat.LeaveAfterHand = true;
                return RoomLeaveStatus.Queued;
            }

            // Persist first. On failure the player remains associated with the room.
            if (!_clearLastRoom(playerName))
                return RoomLeaveStatus.StorageUnavailable;

            _rooms.Leave(roomId, playerName);
            return RoomLeaveStatus.Left;
        }

        public IReadOnlyList<(string PlayerName, RoomLeaveStatus Status)> CompletePending(int roomId)
        {
            var results = new List<(string PlayerName, RoomLeaveStatus Status)>();
            var room = _rooms.FindRoom(roomId);
            if (room == null || _games.IsHandInProgress(roomId))
                return results;

            foreach (var name in room.Seats.Where(seat => seat.LeaveAfterHand)
                .Select(seat => seat.PlayerName).ToArray())
            {
                results.Add((name, Request(roomId, name)));
            }
            return results;
        }
    }
}
