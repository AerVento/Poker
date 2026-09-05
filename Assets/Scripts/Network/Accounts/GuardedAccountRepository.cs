using System;
using System.Collections.Generic;

namespace Game.Network.Accounts
{
    // Expected business rejections are return values. Unexpected repository failures stop the server.
    public sealed class GuardedAccountRepository : IHandAccountRepository,
        IPasswordAccountRepository, ISessionAccountRepository
    {
        private readonly IHandAccountRepository _inner;
        private readonly IPasswordAccountRepository _passwords;
        private readonly ISessionAccountRepository _sessions;
        private readonly Action<Exception> _onFailure;

        public bool HasFailed { get; private set; }
        internal IHandAccountRepository InnerRepository => _inner;

        public GuardedAccountRepository(IHandAccountRepository inner, Action<Exception> onFailure)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _onFailure = onFailure ?? throw new ArgumentNullException(nameof(onFailure));
            _passwords = inner as IPasswordAccountRepository;
            _sessions = inner as ISessionAccountRepository;
        }

        private T Execute<T>(Func<T> operation)
        {
            if (HasFailed)
                throw new InvalidOperationException("Account storage has failed; further access is disabled.");
            try
            {
                return operation();
            }
            catch (Exception exception)
            {
                HasFailed = true;
                _onFailure(exception);
                throw;
            }
        }

        public void Initialize()
        {
            Execute(() => { _inner.Initialize(); return true; });
        }

        public PlayerAccount FindById(string accountId)
        {
            return Execute(() => _inner.FindById(accountId));
        }

        public PlayerAccount FindByUserName(string userName)
        {
            return Execute(() => _inner.FindByUserName(userName));
        }

        public CreateAccountResult Create(PlayerAccount account)
        {
            return Execute(() => _inner.Create(account));
        }

        public bool SetLastRoom(string accountId, int? roomId)
        {
            return Execute(() => _inner.SetLastRoom(accountId, roomId));
        }

        public ChipMutationResult ApplyChipMutation(string accountId, string requestId, string transactionType, long amount, int? roomId, string handId, out PlayerAccount account)
        {
            PlayerAccount snapshot = null;
            var result = Execute(() => _inner.ApplyChipMutation(accountId, requestId, transactionType, amount, roomId, handId, out snapshot));
            account = snapshot;
            return result;
        }

        public HandStartResult BeginHand(string handId, int roomId, IReadOnlyList<string> accountIds, uint minimumChips, out IReadOnlyList<PlayerAccount> accounts)
        {
            IReadOnlyList<PlayerAccount> snapshot = null;
            var result = Execute(() => _inner.BeginHand(handId, roomId, accountIds, minimumChips, out snapshot));
            accounts = snapshot;
            return result;
        }

        public HandDebitResult ApplyHandDebits(string handId, string requestId, IReadOnlyList<HandChipDebit> debits, out IReadOnlyDictionary<string, PlayerAccount> accounts)
        {
            IReadOnlyDictionary<string, PlayerAccount> snapshot = null;
            var result = Execute(() => _inner.ApplyHandDebits(handId, requestId, debits, out snapshot));
            accounts = snapshot;
            return result;
        }

        public HandSettlementResult SettleHand(string handId, string requestId, IReadOnlyList<HandChipCredit> credits, out IReadOnlyDictionary<string, PlayerAccount> accounts)
        {
            IReadOnlyDictionary<string, PlayerAccount> snapshot = null;
            var result = Execute(() =>
            {
                var settlement = _inner.SettleHand(handId, requestId, credits, out snapshot);
                if (settlement != HandSettlementResult.Applied && settlement != HandSettlementResult.AlreadyApplied)
                    throw new InvalidOperationException("Hand settlement was rejected: " + settlement);
                return settlement;
            });
            accounts = snapshot;
            return result;
        }

        public bool AbortHand(string handId, string reason)
        {
            return Execute(() => _inner.AbortHand(handId, reason));
        }

        public bool UpdatePasswordHash(string accountId, PasswordHashData passwordData)
        {
            return Execute(() => _passwords.UpdatePasswordHash(accountId, passwordData));
        }

        public bool CreateSessionToken(string accountId, byte[] tokenHash, DateTime createdAtUtc, DateTime expiresAtUtc)
        {
            return Execute(() => _sessions.CreateSessionToken(accountId, tokenHash, createdAtUtc, expiresAtUtc));
        }

        public SessionTokenRotationResult RotateSessionToken(byte[] currentTokenHash, byte[] replacementTokenHash, DateTime usedAtUtc, DateTime replacementExpiresAtUtc, out PlayerAccount account)
        {
            PlayerAccount snapshot = null;
            var result = Execute(() => _sessions.RotateSessionToken(currentTokenHash, replacementTokenHash, usedAtUtc, replacementExpiresAtUtc, out snapshot));
            account = snapshot;
            return result;
        }

        public bool RevokeSessionToken(byte[] tokenHash, DateTime revokedAtUtc)
        {
            return Execute(() => _sessions.RevokeSessionToken(tokenHash, revokedAtUtc));
        }
    }
}
