using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public enum AccountAuthenticationOperation : byte
    {
        Register,
        Login,
    }

    public struct AccountRegisterRequest : NetworkMessage
    {
        public string UserName;
        public string Password;
    }

    public struct AccountLoginRequest : NetworkMessage
    {
        public string UserName;
        public string Password;
    }

    public struct AccountAuthenticationResponse : NetworkMessage
    {
        public AccountAuthenticationOperation Operation;
        public AccountAuthenticationCode Code;
        public string AccountId;
        public string UserName;
        public uint Chips;
        public bool HasLastRoom;
        public int LastRoomId;
    }
}
