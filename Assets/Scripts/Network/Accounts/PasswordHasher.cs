using System;
using System.Security.Cryptography;

namespace Game.Network.Accounts
{
    public sealed class PasswordHashData
    {
        public string Algorithm { get; set; }

        public byte[] Hash { get; set; }

        public byte[] Salt { get; set; }

        public int Iterations { get; set; }
    }

    public sealed class PasswordHasher
    {
        public const string AlgorithmName = "PBKDF2-HMAC-SHA256";
        public const int DefaultIterations = 600000;
        public const int SaltSize = 16;
        public const int HashSize = 32;

        private readonly int _iterations;

        public PasswordHasher(int iterations = DefaultIterations)
        {
            if (iterations <= 0)
                throw new ArgumentOutOfRangeException(nameof(iterations));

            _iterations = iterations;
        }

        public PasswordHashData HashPassword(string password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            var salt = new byte[SaltSize];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(salt);
            }

            return new PasswordHashData
            {
                Algorithm = AlgorithmName,
                Hash = Derive(password, salt, _iterations),
                Salt = salt,
                Iterations = _iterations,
            };
        }

        public bool Verify(string password, PlayerAccount account)
        {
            if (password == null || account == null)
                return false;
            if (account.PasswordAlgorithm != AlgorithmName ||
                account.PasswordHash == null ||
                account.PasswordSalt == null ||
                account.PasswordIterations <= 0)
            {
                return false;
            }

            var candidate = Derive(password, account.PasswordSalt, account.PasswordIterations);
            return CryptographicOperations.FixedTimeEquals(candidate, account.PasswordHash);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (var deriveBytes = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256))
            {
                return deriveBytes.GetBytes(HashSize);
            }
        }
    }
}
