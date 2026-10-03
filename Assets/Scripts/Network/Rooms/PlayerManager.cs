using Game.Network.Accounts;
using Mirror;
using System;
using System.Collections.Generic;

namespace Game.Network
{
    public sealed class PlayerSession
    {
        public string SessionId { get; set; }

        public PlayerAccount Account { get; set; }

        public NetworkConnectionToClient Connection { get; set; }
    }

    /// <summary>
    /// Stores the server-authoritative mapping between authenticated accounts and connections.
    /// </summary>
    public class PlayerManager
    {
        private readonly Dictionary<string, PlayerSession> _sessionsByUserName =
            new Dictionary<string, PlayerSession>(StringComparer.Ordinal);

        private readonly Dictionary<NetworkConnectionToClient, PlayerSession> _sessionsByConnection =
            new Dictionary<NetworkConnectionToClient, PlayerSession>();

        public NetworkConnectionToClient BindAccount(
            PlayerAccount account,
            NetworkConnectionToClient connection)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            NetworkConnectionToClient replacedConnection = null;
            if (_sessionsByUserName.TryGetValue(account.UserName, out var replacedSession))
            {
                replacedConnection = replacedSession.Connection;
                _sessionsByConnection.Remove(replacedSession.Connection);
            }

            if (_sessionsByConnection.TryGetValue(connection, out var connectionSession))
            {
                _sessionsByConnection.Remove(connection);
                if (_sessionsByUserName.TryGetValue(connectionSession.Account.UserName, out var currentSession) &&
                    ReferenceEquals(currentSession, connectionSession))
                {
                    _sessionsByUserName.Remove(connectionSession.Account.UserName);
                }
            }

            var session = new PlayerSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Account = account.Copy(),
                Connection = connection,
            };
            _sessionsByUserName[account.UserName] = session;
            _sessionsByConnection[connection] = session;
            return replacedConnection;
        }

        public bool IsConnected(string playerName)
        {
            return playerName != null && _sessionsByUserName.ContainsKey(playerName);
        }

        public NetworkConnectionToClient GetConnection(string playerName)
        {
            return playerName != null &&
                _sessionsByUserName.TryGetValue(playerName, out var session)
                ? session.Connection
                : null;
        }

        public bool TryGetSession(NetworkConnectionToClient connection, out PlayerSession session)
        {
            if (connection != null && _sessionsByConnection.TryGetValue(connection, out session))
                return true;

            session = null;
            return false;
        }

        public bool TryGetAccount(NetworkConnectionToClient connection, out PlayerAccount account)
        {
            if (TryGetSession(connection, out var session))
            {
                account = session.Account;
                return true;
            }

            account = null;
            return false;
        }

        public bool TryGetAccount(string playerName, out PlayerAccount account)
        {
            if (playerName != null && _sessionsByUserName.TryGetValue(playerName, out var session))
            {
                account = session.Account;
                return true;
            }

            account = null;
            return false;
        }

        public void SetLastRoom(string playerName, int? roomId)
        {
            if (playerName != null && _sessionsByUserName.TryGetValue(playerName, out var session))
                session.Account.LastRoomId = roomId;
        }

        public bool TryGetPlayerName(NetworkConnectionToClient connection, out string playerName)
        {
            if (TryGetSession(connection, out var session))
            {
                playerName = session.Account.UserName;
                return true;
            }

            playerName = null;
            return false;
        }

        /// <summary>
        /// Removes a connection only when it still owns the current account session.
        /// A replaced connection is already detached and cannot remove its replacement.
        /// </summary>
        public bool RemoveConnection(NetworkConnectionToClient connection, out string playerName)
        {
            if (!TryGetSession(connection, out var session))
            {
                playerName = null;
                return false;
            }

            playerName = session.Account.UserName;
            _sessionsByConnection.Remove(connection);
            if (_sessionsByUserName.TryGetValue(playerName, out var currentSession) &&
                ReferenceEquals(currentSession, session))
            {
                _sessionsByUserName.Remove(playerName);
            }

            return true;
        }
    }
}
