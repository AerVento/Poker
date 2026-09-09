using System;
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
        BalanceRequirementNotMet,
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

    public enum HandSettlementResult
    {
        Applied,
        AlreadyApplied,
        HandNotActive,
        InvalidDistribution,
    }

    public enum SessionTokenRotationResult
    {
        Rotated,
        InvalidToken,
        AccountNotFound,
    }

    public sealed class HandSettlementEntry
    {
        public string AccountId { get; set; }
        public uint Contribution { get; set; }
        public uint Award { get; set; }
        public uint Refund { get; set; }
        public Game.Facts.HandFact OutcomeFact { get; set; }
        // The first participant carries the shared, server-only context into the settlement transaction.
        public Game.Achievements.AchievementContext AchievementContext { get; set; }
    }

    public sealed class HandSettlementBalance
    {
        public string AccountId { get; set; }
        public uint StartingChips { get; set; }
        public uint Chips { get; set; }
        // Chips is the immutable hand result; AccountChips also includes independently claimed rewards.
        public uint? AccountChips { get; set; }
    }

    public sealed class ChipReconciliationReport
    {
        public long AccountBalanceTotal { get; set; }

        public long LedgerNetTotal { get; set; }

        public long ActiveContributionTotal { get; set; }

        public long InternalHandLedgerTotal { get; set; }

        public long ActiveHandCount { get; set; }

        public long LedgerEntryCount { get; set; }

        public long InvalidLedgerEntryCount { get; set; }

        public long AccountLedgerMismatchCount { get; set; }

        public long InvalidActiveContributionCount { get; set; }

        public bool IsConsistent =>
            AccountBalanceTotal == LedgerNetTotal &&
            InternalHandLedgerTotal + ActiveContributionTotal == 0 &&
            InvalidLedgerEntryCount == 0 &&
            AccountLedgerMismatchCount == 0 &&
            InvalidActiveContributionCount == 0;
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

    public interface IPasswordAccountRepository
    {
        bool UpdatePasswordHash(string accountId, PasswordHashData passwordData);
    }

    public interface IHandAccountRepository : IAccountRepository
    {
        HandStartResult BeginHand(
            string handId,
            int roomId,
            IReadOnlyList<string> accountIds,
            uint minimumChips,
            out IReadOnlyList<PlayerAccount> accounts);

        HandSettlementResult SettleHand(
            string handId,
            string requestId,
            IReadOnlyList<HandSettlementEntry> entries,
            out IReadOnlyDictionary<string, HandSettlementBalance> accounts);

        bool AbortHand(string handId, string reason);
    }

    public interface ISessionAccountRepository
    {
        bool CreateSessionToken(
            string accountId,
            byte[] tokenHash,
            DateTime createdAtUtc,
            DateTime expiresAtUtc);

        SessionTokenRotationResult RotateSessionToken(
            byte[] currentTokenHash,
            byte[] replacementTokenHash,
            DateTime usedAtUtc,
            DateTime replacementExpiresAtUtc,
            out PlayerAccount account);

        bool RevokeSessionToken(byte[] tokenHash, DateTime revokedAtUtc);
    }
}
