using Mirror;

namespace Game.Network
{
    public enum TableParticipationAction : byte
    {
        JoinNextHand,
        KeepSpectating,
        SitOutAfterHand,
        CancelSitOut,
    }

    public enum TableParticipationResult : byte
    {
        Success,
        RoomUnavailable,
        NotMember,
        TableFull,
        InsufficientChips,
        StorageUnavailable,
        InvalidAction,
        StaleRequest,
    }

    public struct TableParticipationRequest : NetworkMessage
    {
        public int RoomId;
        public string HandId;
        public uint Revision;
        public TableParticipationAction Action;
    }

    public struct TableParticipationResponse : NetworkMessage
    {
        public int RoomId;
        public TableParticipationResult Result;
    }
}
