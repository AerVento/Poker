using Mirror;
using Game.Network.Accounts;

namespace Game.Network
{
    public enum SlotLotteryAction : byte { Status, Draw, Acknowledge, Pool }
    public struct SlotLotteryRequest : NetworkMessage
    {
        public ulong RequestId;
        public SlotLotteryAction Action;
        public string OperationId;
        public SlotPrizeTier Tier;
        public int Offset;
        public string Revision;
        public byte Count;
    }
    public struct SlotLotteryResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public SlotLotteryAction Action;
        public SlotLotteryResult Result;
        public string Revision;
        public bool Enabled;
        public uint CostDollars;
        public uint Dollars;
        public uint Chips;
        public uint AvailableChips;
        public bool CanExchange;
        public SlotDrawReceipt Receipt;
        public SlotLotteryDisclosure Disclosure;
    }
}
