using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public struct WalletRequest : NetworkMessage
    {
        public ulong RequestId;
        public bool IsExchange;
        public string OperationId;
        public bool DollarsToChips;
        public uint Amount;
    }

    public struct WalletResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public bool IsExchange;
        public string OperationId;
        public WalletResult Result;
        public bool HasSnapshot;
        public uint Dollars;
        public uint Chips;
        public uint AvailableChips;
        public bool CanExchange;
    }
}
