using System;

namespace Game
{
    // Unified IDs are server-side identities; existing cardFace messages still carry the original ID.
    public static class CardFaceItemId
    {
        public static System.Collections.Generic.IReadOnlyList<string> DefaultOwnedIds { get; } =
            Array.AsReadOnly(new[] { "default", "minimal" });

        public static bool IsDefaultOwned(string id) => id == "default" || id == "minimal";

        public const string Prefix = "cardface-";
        public const int MaxCardFaceIdLength = 48;
        public const int MaxItemIdLength = MaxCardFaceIdLength + 9;

        public static string ToItemId(string cardFaceId)
        {
            if (!TryToItemId(cardFaceId, out var itemId))
                throw new ArgumentException("Invalid cardFace ID.", nameof(cardFaceId));
            return itemId;
        }

        public static string ToCardFaceId(string itemId)
        {
            if (!TryToCardFaceId(itemId, out var cardFaceId))
                throw new ArgumentException("Not a valid cardFace item ID.", nameof(itemId));
            return cardFaceId;
        }

        public static bool TryToItemId(string cardFaceId, out string itemId)
        {
            itemId = null;
            if (!IsValidCardFaceId(cardFaceId))
                return false;
            itemId = Prefix + cardFaceId;
            return true;
        }

        public static bool TryToCardFaceId(string itemId, out string cardFaceId)
        {
            cardFaceId = null;
            if (itemId == null || itemId.Length > MaxItemIdLength || !itemId.StartsWith(Prefix, StringComparison.Ordinal))
                return false;
            var candidate = itemId.Substring(Prefix.Length);
            if (!IsValidCardFaceId(candidate))
                return false;
            cardFaceId = candidate;
            return true;
        }

        private static bool IsValidCardFaceId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > MaxCardFaceIdLength)
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
