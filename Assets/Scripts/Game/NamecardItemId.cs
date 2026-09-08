using System;

namespace Game
{
    // Unified IDs are server-side identities; existing namecard messages still carry the original ID.
    public static class NamecardItemId
    {
        public const string Prefix = "namecard-";
        public const int MaxNamecardIdLength = 48;
        public const int MaxItemIdLength = MaxNamecardIdLength + 9;

        public static string ToItemId(string namecardId)
        {
            if (!TryToItemId(namecardId, out var itemId))
                throw new ArgumentException("Invalid namecard ID.", nameof(namecardId));
            return itemId;
        }

        public static string ToNamecardId(string itemId)
        {
            if (!TryToNamecardId(itemId, out var namecardId))
                throw new ArgumentException("Not a valid namecard item ID.", nameof(itemId));
            return namecardId;
        }

        public static bool TryToItemId(string namecardId, out string itemId)
        {
            itemId = null;
            if (!IsValidNamecardId(namecardId))
                return false;
            itemId = Prefix + namecardId;
            return true;
        }

        public static bool TryToNamecardId(string itemId, out string namecardId)
        {
            namecardId = null;
            if (itemId == null || itemId.Length > MaxItemIdLength || !itemId.StartsWith(Prefix, StringComparison.Ordinal))
                return false;
            var candidate = itemId.Substring(Prefix.Length);
            if (!IsValidNamecardId(candidate))
                return false;
            namecardId = candidate;
            return true;
        }

        private static bool IsValidNamecardId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > MaxNamecardIdLength)
                return false;
            foreach (var character in id)
            {
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') &&
                    character != '_' && character != '-')
                    return false;
            }
            return true;
        }
    }
}
