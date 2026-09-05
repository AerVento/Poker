using Mirror;

namespace Game.Network
{
    public enum ServerShutdownReason : byte
    {
        StorageFailure,
    }

    public struct ServerShutdownNotice : NetworkMessage
    {
        public ServerShutdownReason Reason;
    }
}
