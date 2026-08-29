using System.Collections.Generic;

namespace Game.Network.Accounts
{
    public enum CreateAccountResult
    {
        Created,
        UserNameTaken,
    }

    public enum ChipMutationResult
    {
        Applied,
        AlreadyApplied,
        AccountNotFound,
        AccountBusyInHand,
        InsufficientChips,
        Overflow,
    }

    public enum HandStartResult
    {
        Started,
        AlreadyExists,
        AccountNotFound,
        AccountAlreadyInHand,
        InsufficientChips,
        BalanceLimitExceeded,
    }

    public enum HandDebitResult
    {
        Applied,
        AlreadyApplied,
        HandNotActive,
        AccountNotInHand,
        InsufficientChips,
    }

    public enum HandSettlementResult
    {
        Applied,
        AlreadyApplied,
        HandNotActive,
        InvalidDistribution,
    }

    public sealed class HandChipDebit
    {
        public string AccountId { get; set; }

        public uint Amount { get; set; }
    }

    public sealed class HandChipCredit
    {
        public string AccountId { get; set; }

        public uint Amount { get; set; }
    }

    public interface IAccountRepository
    {
        void Initialize();

        PlayerAccount FindById(string accountId);

        PlayerAccount FindByUserName(string userName);

        CreateAccountResult Create(PlayerAccount account);

        bool SetLastRoom(string accountId, int? roomId);

        ChipMutationResult ApplyChipMutation(
            string accountId,
            string requestId,
            string transactionType,
            long amount,
            int? roomId,
            string handId,
            out PlayerAccount account);
    }

    public interface IHandAccountRepository : IAccountRepository
    {
        HandStartResult BeginHand(
            string handId,
            int roomId,
            IReadOnlyList<string> accountIds,
            uint minimumChips,
            out IReadOnlyList<PlayerAccount> accounts);

        HandDebitResult ApplyHandDebits(
            string handId,
            string requestId,
            IReadOnlyList<HandChipDebit> debits,
            out IReadOnlyDictionary<string, PlayerAccount> accounts);

        HandSettlementResult SettleHand(
            string handId,
            string requestId,
            IReadOnlyList<HandChipCredit> credits,
            out IReadOnlyDictionary<string, PlayerAccount> accounts);

        bool AbortHand(string handId, string reason);
    }
}
