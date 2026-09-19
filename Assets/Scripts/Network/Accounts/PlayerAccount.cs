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

        public string EquippedNamecardId { get; set; } = string.Empty;

        public string EquippedCardFaceId { get; set; } = string.Empty;

        public uint Chips { get; set; }

        public uint Dollars { get; set; }

        public ulong TotalAssets => (ulong)Dollars + Chips;

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
                Dollars = Dollars,
                EquippedNamecardId = EquippedNamecardId,
                EquippedCardFaceId = EquippedCardFaceId,
                LastRoomId = LastRoomId,
                Version = Version,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = UpdatedAtUtc,
            };
        }
    }
}
