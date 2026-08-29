using System;

namespace Game.Network.Accounts
{
    public static class AccountInputValidator
    {
        public const int MaximumUserNameLength = 32;
        public const int MinimumPasswordLength = 8;
        public const int MaximumPasswordLength = 128;

        public static bool IsValidUserName(string userName)
        {
            if (string.IsNullOrEmpty(userName) || userName.Length > MaximumUserNameLength)
                return false;
            if (char.IsWhiteSpace(userName[0]) || char.IsWhiteSpace(userName[userName.Length - 1]))
                return false;

            foreach (var character in userName)
            {
                if (char.IsControl(character))
                    return false;
            }

            return true;
        }

        public static bool IsValidPassword(string password)
        {
            return password != null &&
                password.Length >= MinimumPasswordLength &&
                password.Length <= MaximumPasswordLength;
        }
    }
}
