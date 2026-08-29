using System;

namespace Game.Network.Accounts
{
    public sealed class PlayerAccount
    {
        public string AccountId { get; set; }

        public string UserName { get; set; }

        public string PasswordAlgorithm { get; set; }

        public byte[] PasswordHash { get; set; }

        public byte[] PasswordSalt { get; set; }

        public int PasswordIterations { get; set; }

        public long Chips { get; set; }

        public int? LastRoomId { get; set; }

        public long Version { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        public PlayerAccount Copy()
        {
            return new PlayerAccount
            {
                AccountId = AccountId,
                UserName = UserName,
                PasswordAlgorithm = PasswordAlgorithm,
                PasswordHash = PasswordHash == null ? null : (byte[])PasswordHash.Clone(),
                PasswordSalt = PasswordSalt == null ? null : (byte[])PasswordSalt.Clone(),
                PasswordIterations = PasswordIterations,
                Chips = Chips,
                LastRoomId = LastRoomId,
                Version = Version,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = UpdatedAtUtc,
            };
        }
    }
}
