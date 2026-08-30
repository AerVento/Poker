using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Game.Network
{
    public enum TableParticipationStatus : byte
    {
        Playing,
        WaitingForNextHand,
        Spectating,
        InsufficientChips,
    }

    public struct GameInfoSync : NetworkMessage
    {
        public string RawData;
        public uint AccountChips;
        public uint BigBlind;
        public TableParticipationStatus ParticipationStatus;
        public ClientGameInfo Info
        {
            get => JsonSerializer.Deserialize<ClientGameInfo>(RawData);
            set => RawData = JsonSerializer.Serialize(value);
        }
    }

    public struct InterHandCountdownSync : NetworkMessage
    {
        public int RoomId;
        public bool IsActive;
        public int SecondsRemaining;
    }

    public struct AccountBalanceSync : NetworkMessage
    {
        public uint Chips;
    }
}
