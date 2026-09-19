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
        public bool Hidden;
        public uint RewardDollars;
        public TaskRewardItemState[] RewardItems;
        public uint RewardChips => RewardDollars;
    }

    // Definitions are shared across accounts; player state never repeats display metadata.
    public struct AchievementDefinitionView
    {
        public string Id;
        public string Name;
        public string Description;
        public string Category;
        public string IconPath;
        public int Target;
        public bool Hidden;
        public uint RewardDollars;
        public TaskRewardItemState[] RewardItems;
        public uint RewardChips => RewardDollars;
    }

    public struct AchievementStateView
    {
        public string Id;
        public int Progress;
        public long CompletedAt;
        public long ClaimedAt;
        // Completed achievements retain the reward snapshot taken when they unlocked.
        public uint RewardDollars;
        public TaskRewardItemState[] RewardItems;
        public bool HasRevealedDefinition;
        public AchievementDefinitionView RevealedDefinition;
        public uint RewardChips => RewardDollars;
    }

    public struct AchievementCatalogRequest : NetworkMessage
    {
        public ulong RequestId;
        public int Offset;
        public string Revision;
    }

    public struct AchievementCatalogResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public AchievementResult Result;
        public string Revision;
        public int Offset;
        public int NextOffset;
        public int Total;
        public AchievementDefinitionView[] Definitions;
    }

    public struct AchievementStatesRequest : NetworkMessage
    {
        public ulong RequestId;
        public int Offset;
        public long Revision;
        public string CatalogRevision;
    }

    public struct AchievementStatesResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public AchievementResult Result;
        public long Revision;
        public string CatalogRevision;
        public int Offset;
        public int NextOffset;
        public int Total;
        public AchievementStateView[] States;
    }

    public struct AchievementProgressChanged : NetworkMessage
    {
        public bool Changed;
    }

    // Retained for existing diagnostics; the normal panel uses the split protocol above.
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
        public uint Dollars;
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
        public uint GrantedDollars;
        public TaskRewardItemState[] GrantedItems;
        public bool HasState;
        public long Revision;
        public AchievementStateView State;
        public uint GrantedChips => GrantedDollars;
    }

    public struct AchievementBatchClaimRequest : NetworkMessage
    {
        public ulong RequestId;
        // Empty means every category; otherwise this is an exact catalog category.
        public string Category;
    }

    public struct AchievementBatchClaimResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public string Category;
        public AchievementResult Result;
        public int ClaimedCount;
        public uint GrantedDollars;
        public TaskRewardItemState[] GrantedItems;
        public long Revision;
        public AchievementStateView[] States;
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
