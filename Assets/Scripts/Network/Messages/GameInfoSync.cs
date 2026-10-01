using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        // Serialized by Serialization.WriteClientGameInfo, so receivers decode it once per message.
        public ClientGameInfo Info;
        public uint AccountChips;
        public bool CanClaimDailyTaskRewards;
        public uint BigBlind;
        public TableParticipationStatus ParticipationStatus;
        public bool CanLeaveImmediately;
        public bool LeaveAfterHand;
        public bool SitOutAfterHand;
        public bool JoinNextHand;
        public bool CanJoinNextHand;
        public uint ParticipationRevision;
    }

    public struct InterHandCountdownSync : NetworkMessage
    {
        public int RoomId;
        public bool IsActive;
        public int SecondsRemaining;
        public string HandId;
        public bool CanStartNextHand;
    }

    public struct AccountBalanceSync : NetworkMessage
    {
        public uint Dollars;
        public uint Chips;
    }
}
