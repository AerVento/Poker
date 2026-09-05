using Mirror;

namespace Game.Network
{
    public enum RoomLeaveStatus : byte
    {
        Left,
        Queued,
        Cancelled,
        RoomNotFound,
        NotMember,
        StorageUnavailable,
        TableFull,
    }

    public struct PlayerLeaveRoomResponse : NetworkMessage
    {
        public int RoomId;
        public RoomLeaveStatus Status;
        public RoomTransitionReason TransitionReason;
    }
}
