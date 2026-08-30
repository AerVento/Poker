using UnityEngine;

namespace Game
{
    public static class LoginCredentialStore
    {
        private const string UserNameKey = "Account_UserName";
        private const string SessionTokenKey = "Account_SessionToken";

        public static string SavedUserName => PlayerPrefs.GetString(UserNameKey, string.Empty);

        public static bool TryLoad(out string userName, out string sessionToken)
        {
            userName = PlayerPrefs.GetString(UserNameKey, string.Empty);
            sessionToken = PlayerPrefs.GetString(SessionTokenKey, string.Empty);
            return !string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(sessionToken);
        }

        public static void Save(string userName, string sessionToken)
        {
            PlayerPrefs.SetString(UserNameKey, userName);
            PlayerPrefs.SetString(SessionTokenKey, sessionToken);
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(UserNameKey);
            PlayerPrefs.DeleteKey(SessionTokenKey);
            PlayerPrefs.Save();
        }

        public static bool ClearIfMatches(string sessionToken)
        {
            if (string.IsNullOrEmpty(sessionToken) ||
                PlayerPrefs.GetString(SessionTokenKey, string.Empty) != sessionToken)
            {
                return false;
            }

            Clear();
            return true;
        }
    }
}
