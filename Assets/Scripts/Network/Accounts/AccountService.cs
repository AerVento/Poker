using System;
using System.Security.Cryptography;
using System.Text;

namespace Game.Network.Accounts
{
    public enum AccountAuthenticationCode
    {
        Success,
        InvalidUserName,
        InvalidPassword,
        UserNameTaken,
        InvalidCredentials,
        SessionExpired,
        StorageUnavailable,
    }

    public sealed class AccountAuthenticationResult
    {
        public AccountAuthenticationCode Code { get; set; }

        public PlayerAccount Account { get; set; }

        public string SessionToken { get; set; }

        public bool IsSuccess => Code == AccountAuthenticationCode.Success;
    }

    public sealed class AccountService
    {
        public const uint DefaultInitialChips = 10000;

        public static readonly TimeSpan DefaultSessionLifetime = TimeSpan.FromDays(30);

        private readonly IAccountRepository _repository;
        private readonly PasswordHasher _passwordHasher;
        private readonly IPasswordAccountRepository _passwordRepository;
        private readonly ISessionAccountRepository _sessionRepository;
        private readonly uint _initialChips;
        private readonly TimeSpan _sessionLifetime;
        private readonly Func<DateTime> _utcNow;

        public AccountService(
            IAccountRepository repository,
            PasswordHasher passwordHasher = null,
            uint initialChips = DefaultInitialChips,
            TimeSpan? sessionLifetime = null,
            Func<DateTime> utcNow = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _sessionRepository = repository as ISessionAccountRepository;
            _passwordRepository = repository as IPasswordAccountRepository;
            _passwordHasher = passwordHasher ?? new PasswordHasher();
            _initialChips = initialChips;
            _sessionLifetime = sessionLifetime ?? DefaultSessionLifetime;
            if (_sessionLifetime <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(sessionLifetime));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public void Initialize()
        {
            _repository.Initialize();
        }

        public AccountAuthenticationResult Register(string userName, string password)
        {
            if (!AccountInputValidator.IsValidUserName(userName))
                return Failure(AccountAuthenticationCode.InvalidUserName);
            if (!AccountInputValidator.IsValidPassword(password))
                return Failure(AccountAuthenticationCode.InvalidPassword);

            try
            {
                var passwordData = _passwordHasher.HashPassword(password);
                var now = DateTime.UtcNow;
                var account = new PlayerAccount
                {
                    AccountId = Guid.NewGuid().ToString("N"),
                    UserName = userName,
                    PasswordAlgorithm = passwordData.Algorithm,
                    PasswordHash = passwordData.Hash,
                    PasswordSalt = passwordData.Salt,
                    PasswordIterations = passwordData.Iterations,
                    Chips = _initialChips,
                    Version = 0,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                };

                if (_repository.Create(account) == CreateAccountResult.UserNameTaken)
                    return Failure(AccountAuthenticationCode.UserNameTaken);

                return CreateSession(account);
            }
            catch
            {
                return Failure(AccountAuthenticationCode.StorageUnavailable);
            }
        }

        public AccountAuthenticationResult Login(string userName, string password)
        {
            if (!AccountInputValidator.IsValidUserName(userName) || password == null)
                return Failure(AccountAuthenticationCode.InvalidCredentials);

            try
            {
                var account = _repository.FindByUserName(userName);
                if (account == null || !_passwordHasher.Verify(password, account))
                    return Failure(AccountAuthenticationCode.InvalidCredentials);

                MigratePasswordWorkFactor(account, password);

                return CreateSession(account);
            }
            catch
            {
                return Failure(AccountAuthenticationCode.StorageUnavailable);
            }
        }

        public AccountAuthenticationResult LoginWithToken(string sessionToken)
        {
            if (!IsValidSessionToken(sessionToken) || _sessionRepository == null)
                return Failure(AccountAuthenticationCode.SessionExpired);

            try
            {
                var replacementToken = GenerateSessionToken();
                var now = _utcNow().ToUniversalTime();
                var result = _sessionRepository.RotateSessionToken(
                    HashSessionToken(sessionToken),
                    HashSessionToken(replacementToken),
                    now,
                    now + _sessionLifetime,
                    out var account);
                if (result != SessionTokenRotationResult.Rotated || account == null)
                    return Failure(AccountAuthenticationCode.SessionExpired);

                return Success(account, replacementToken);
            }
            catch
            {
                return Failure(AccountAuthenticationCode.StorageUnavailable);
            }
        }

        public bool Logout(string sessionToken)
        {
            if (!IsValidSessionToken(sessionToken) || _sessionRepository == null)
                return false;

            try
            {
                return _sessionRepository.RevokeSessionToken(
                    HashSessionToken(sessionToken),
                    _utcNow().ToUniversalTime());
            }
            catch
            {
                return false;
            }
        }

        private void MigratePasswordWorkFactor(PlayerAccount account, string password)
        {
            if (_passwordRepository == null ||
                account.PasswordAlgorithm != PasswordHasher.AlgorithmName ||
                account.PasswordIterations == _passwordHasher.Iterations)
            {
                return;
            }

            try
            {
                var passwordData = _passwordHasher.HashPassword(password);
                if (!_passwordRepository.UpdatePasswordHash(account.AccountId, passwordData))
                    return;

                account.PasswordAlgorithm = passwordData.Algorithm;
                account.PasswordHash = passwordData.Hash;
                account.PasswordSalt = passwordData.Salt;
                account.PasswordIterations = passwordData.Iterations;
            }
            catch
            {
                // A work-factor migration must not reject otherwise valid credentials.
            }
        }

        private AccountAuthenticationResult CreateSession(PlayerAccount account)
        {
            if (_sessionRepository == null)
                return Failure(AccountAuthenticationCode.StorageUnavailable);

            var sessionToken = GenerateSessionToken();
            var now = _utcNow().ToUniversalTime();
            if (!_sessionRepository.CreateSessionToken(
                    account.AccountId,
                    HashSessionToken(sessionToken),
                    now,
                    now + _sessionLifetime))
            {
                return Failure(AccountAuthenticationCode.StorageUnavailable);
            }

            return Success(account, sessionToken);
        }

        private static AccountAuthenticationResult Success(PlayerAccount account, string sessionToken)
        {
            return new AccountAuthenticationResult
            {
                Code = AccountAuthenticationCode.Success,
                Account = account,
                SessionToken = sessionToken,
            };
        }

        private static string GenerateSessionToken()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static byte[] HashSessionToken(string sessionToken)
        {
            using (var sha256 = SHA256.Create())
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(sessionToken));
        }

        private static bool IsValidSessionToken(string sessionToken)
        {
            return !string.IsNullOrEmpty(sessionToken) && sessionToken.Length <= 256;
        }

        private static AccountAuthenticationResult Failure(AccountAuthenticationCode code)
        {
            return new AccountAuthenticationResult { Code = code };
        }
    }
}
