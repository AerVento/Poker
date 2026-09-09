using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public struct AchievementView
    {
        public string Id;
        public string Name;
        public string Description;
        public string Category;
        public string IconPath;
        public int Progress;
        public int Target;
        public long CompletedAt;
        public long ClaimedAt;
        public uint RewardChips;
        public TaskRewardItemState[] RewardItems;
    }

    public struct AchievementsRequest : NetworkMessage
    {
        public ulong RequestId;
        public int Offset;
        public long Revision;
    }

    public struct AchievementsResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public AchievementResult Result;
        public long Revision;
        public int Offset;
        public int NextOffset;
        public int Total;
        public bool CanClaim;
        public uint Balance;
        public AchievementView[] Achievements;
    }

    public struct AchievementClaimRequest : NetworkMessage
    {
        public ulong RequestId;
        public string AchievementId;
    }

    public struct AchievementClaimResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public string AchievementId;
        public AchievementResult Result;
        public uint GrantedChips;
        public TaskRewardItemState[] GrantedItems;
    }

    public struct AchievementUnlocked : NetworkMessage
    {
        public string AccountId;
        public AchievementView Achievement;
    }

    public struct AchievementAnnouncementAck : NetworkMessage
    {
        public string AchievementId;
    }
}
