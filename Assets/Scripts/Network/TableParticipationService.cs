using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Network
{
    // Room membership and a reservation for the next hand do not allocate a seat.
    public sealed class TableParticipationService
    {
        private readonly RoomManager _rooms;
        private readonly GameManager _games;
        private readonly Func<string, uint?> _getChips;
        private readonly Func<string, bool> _isConnected;
        private readonly Dictionary<string, uint> _revisions = new Dictionary<string, uint>();

        public TableParticipationService(RoomManager rooms, GameManager games,
            Func<string, uint?> getChips, Func<string, bool> isConnected)
        {
            _rooms = rooms;
            _games = games;
            _getChips = getChips;
            _isConnected = isConnected;
        }

        public uint GetRevision(string playerName) =>
            _revisions.TryGetValue(playerName, out var revision) ? revision : 0;

        private bool IsEligible(RoomInfo room, string name)
        {
            return _isConnected(name) && _getChips(name) >= checked((uint)room.SmallBlindChips * 2U);
        }

        public int CountNextHandPlayers(int roomId)
        {
            var room = _rooms.FindRoom(roomId);
            if (room == null)
                return 0;
            return room.Seats.Count(seat => !seat.LeaveAfterHand && !seat.SitOutAfterHand &&
                seat.State != RoomSeatState.Watching && IsEligible(room, seat.PlayerName)) +
                room.NextHandPlayers.Count(name => room.Spectators.Contains(name) && IsEligible(room, name));
        }

        private static int ReservedPlayerCount(RoomInfo room) =>
            room.Seats.Count(seat => !seat.LeaveAfterHand && !seat.SitOutAfterHand) + room.NextHandPlayers.Count;

        public TableParticipationResult CanJoin(int roomId, string name)
        {
            var room = _rooms.FindRoom(roomId);
            if (room == null || room.IsWaiting)
                return TableParticipationResult.RoomUnavailable;
            if (!room.Spectators.Contains(name))
                return TableParticipationResult.NotMember;
            var chips = _getChips(name);
            if (!chips.HasValue)
                return TableParticipationResult.StorageUnavailable;
            if (chips.Value < checked((uint)room.SmallBlindChips * 2U))
                return TableParticipationResult.InsufficientChips;
            if (!room.NextHandPlayers.Contains(name) && ReservedPlayerCount(room) >= room.MaxPlayerCount)
                return TableParticipationResult.TableFull;
            return TableParticipationResult.Success;
        }

        public TableParticipationResult Request(int roomId, string name,
            TableParticipationAction action, string handId, uint revision)
        {
            var room = _rooms.FindRoom(roomId);
            var game = _games.GetGameInfo(roomId);
            if (room == null || room.IsWaiting || game == null)
                return TableParticipationResult.RoomUnavailable;
            if (!room.ContainsMember(name))
                return TableParticipationResult.NotMember;
            if (handId != game.HandId || revision != GetRevision(name))
                return TableParticipationResult.StaleRequest;

            var seat = room.FindSeat(name);
            switch (action)
            {
                case TableParticipationAction.JoinNextHand:
                    var result = CanJoin(roomId, name);
                    if (result != TableParticipationResult.Success)
                        return result;
                    if (!room.NextHandPlayers.Contains(name))
                        room.NextHandPlayers.Add(name);
                    break;
                case TableParticipationAction.KeepSpectating:
                    if (seat != null)
                        return TableParticipationResult.InvalidAction;
                    room.NextHandPlayers.Remove(name);
                    break;
                case TableParticipationAction.SitOutAfterHand:
                    if (seat == null || seat.LeaveAfterHand)
                        return TableParticipationResult.InvalidAction;
                    if (_games.RequiresDeferredLeave(roomId, name))
                        seat.SitOutAfterHand = true;
                    else
                        _rooms.MoveToSpectator(roomId, name);
                    break;
                case TableParticipationAction.CancelSitOut:
                    if (seat == null || !seat.SitOutAfterHand)
                        return TableParticipationResult.InvalidAction;
                    // Someone may already have reserved the place being vacated.
                    if (ReservedPlayerCount(room) >= room.MaxPlayerCount)
                        return TableParticipationResult.TableFull;
                    seat.SitOutAfterHand = false;
                    break;
                default:
                    return TableParticipationResult.InvalidAction;
            }

            _revisions[name] = checked(GetRevision(name) + 1);
            return TableParticipationResult.Success;
        }

        public void AdmitForNextHand(int roomId)
        {
            var room = _rooms.FindRoom(roomId);
            if (room == null || _games.IsHandInProgress(roomId))
                return;
            foreach (var name in room.NextHandPlayers.ToArray())
            {
                if (!room.Spectators.Contains(name) || !IsEligible(room, name))
                {
                    room.NextHandPlayers.Remove(name);
                    continue;
                }
                var seatIndex = room.FindAvailableSeatIndex();
                if (seatIndex < 0)
                    break;
                room.Spectators.Remove(name);
                room.NextHandPlayers.Remove(name);
                room.Seats.Add(new RoomSeatInfo
                {
                    SeatIndex = seatIndex,
                    PlayerName = name,
                    State = RoomSeatState.WaitingForNextHand,
                });
                room.SynchronizeLegacyLists();
            }
        }
    }
}
