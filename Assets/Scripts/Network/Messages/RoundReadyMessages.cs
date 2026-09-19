using Mirror;

namespace Game.Network
{
    public struct RoundReadyRequest : NetworkMessage
    {
        public int RoomId;
        public string HandId;
    }

    public struct RevealLastHandRequest : NetworkMessage
    {
        public int RoomId;
        public string HandId;
    }

    public struct RoundReadySync : NetworkMessage
    {
        public int RoomId;
        public string HandId;
        public int RequiredCount;
        public int ConfirmedCount;
        public bool RequiresConfirmation;
        public bool IsConfirmed;
        public bool CanReveal;
    }

    // A public result amendment; receiving it never automatically opens a result panel.
    public struct InsufficientChipsNotice : NetworkMessage
    {
        public int RoomId;
        public uint Chips;
        public uint Dollars;
        public uint BigBlind;
    }

    public struct RoundResultUpdate : NetworkMessage
    {
        public int RoomId;
        public string RawData;
    }
}
