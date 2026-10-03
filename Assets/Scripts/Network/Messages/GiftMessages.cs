using Mirror;

namespace Game.Network
{
    public struct GiftPlaybackCommandMessage
    {
        public string ItemId;
        public int SenderSeatIndex;
        public int TargetSeatIndex;
        public uint Quantity;
    }

    public struct GiftPlaybackBatchMessage : NetworkMessage
    {
        public string EventId;
        public double StartAtNetworkTime;
        public GiftPlaybackCommandMessage[] Commands;
    }
}
