using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public enum AccountAuthenticationOperation : byte
    {
        Register,
        Login,
        TokenLogin,
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

    public struct AccountTokenLoginRequest : NetworkMessage
    {
        public string SessionToken;
    }

    public struct AccountAuthenticationResponse : NetworkMessage
    {
        public AccountAuthenticationOperation Operation;
        public AccountAuthenticationCode Code;
        public string AccountId;
        public string UserName;
        public uint Chips;
        public string EquippedCardFaceId;
        public bool HasLastRoom;
        public int LastRoomId;
        public bool RequiresRoomRecovery;
        public string SessionToken;
    }

    public struct AccountLogoutRequest : NetworkMessage
    {
        public string SessionToken;
    }

    public struct AccountLogoutResponse : NetworkMessage
    {
        public bool Revoked;
    }


    public struct PlayerRoomRecoveryRequest : NetworkMessage
    {
        public int RoomId;
        public bool ReturnToRoom;
    }
}
