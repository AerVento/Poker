using System;

namespace Game.Network.Accounts
{
    public enum AccountAuthenticationCode
    {
        Success,
        InvalidUserName,
        InvalidPassword,
        UserNameTaken,
        InvalidCredentials,
        StorageUnavailable,
    }

    public sealed class AccountAuthenticationResult
    {
        public AccountAuthenticationCode Code { get; set; }

        public PlayerAccount Account { get; set; }

        public bool IsSuccess => Code == AccountAuthenticationCode.Success;
    }

    public sealed class AccountService
    {
        public const uint DefaultInitialChips = 10000;

        private readonly IAccountRepository _repository;
        private readonly PasswordHasher _passwordHasher;
        private readonly uint _initialChips;

        public AccountService(
            IAccountRepository repository,
            PasswordHasher passwordHasher = null,
            uint initialChips = DefaultInitialChips)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _passwordHasher = passwordHasher ?? new PasswordHasher();
            _initialChips = initialChips;
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

                return Success(account);
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

                return Success(account);
            }
            catch
            {
                return Failure(AccountAuthenticationCode.StorageUnavailable);
            }
        }

        private static AccountAuthenticationResult Success(PlayerAccount account)
        {
            return new AccountAuthenticationResult
            {
                Code = AccountAuthenticationCode.Success,
                Account = account,
            };
        }

        private static AccountAuthenticationResult Failure(AccountAuthenticationCode code)
        {
            return new AccountAuthenticationResult { Code = code };
        }
    }
}
