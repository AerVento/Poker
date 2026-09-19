using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public struct GiftInventoryRequest : NetworkMessage
    {
        public ulong RequestId;
    }

    public struct GiftInventoryResponse : NetworkMessage
    {
        public ulong RequestId;
        public GiftUseResult Result;
        public ItemStackMessage[] Items;
        public uint Dollars;
        public bool CanSend;
        public GiftOfferMessage[] Offers;
    }

    public struct GiftOfferMessage
    {
        public string ItemId;
        public uint Price;
        public bool CanPurchase;
    }

    public struct GiftUseCommandMessage
    {
        public string ItemId;
        public int TargetSeatIndex;
        public uint Quantity;
    }

    public struct GiftUseBatchRequest : NetworkMessage
    {
        public ulong RequestId;
        public string OperationId;
        public GiftUseCommandMessage[] Commands;
    }

    public struct GiftUseBatchResponse : NetworkMessage
    {
        public ulong RequestId;
        public string OperationId;
        public GiftUseResult Result;
        public bool IsNewlyApplied;
        public bool HasSnapshot;
        public ItemStackMessage[] Items;
        public uint Dollars;
        public bool CanSend;
        public GiftOfferMessage[] Offers;
    }

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
