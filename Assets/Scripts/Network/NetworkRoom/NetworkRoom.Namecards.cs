using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        // Room members see each other's equipped namecard item and service years.
        private RoomInfo PopulateNamecards(RoomInfo room)
        {
            if (room == null)
                return null;
            if (room.Namecards == null)
                room.Namecards = new Dictionary<string, string>();
            if (room.ServiceYears == null)
                room.ServiceYears = new Dictionary<string, ulong>();
            var visibleNames = new HashSet<string>(room.MemberNames);
            var game = _gameManager.GetGameInfo(room.Id);
            if (game != null)
                foreach (var player in game.Players)
                    if (player.IsOccupied) visibleNames.Add(player.PlayerName);
            foreach (var stale in room.Namecards.Keys.Where(name => !visibleNames.Contains(name)).ToArray())
                room.Namecards.Remove(stale);
            foreach (var stale in room.ServiceYears.Keys.Where(name => !visibleNames.Contains(name)).ToArray())
                room.ServiceYears.Remove(stale);
            // Keep disconnected / departed hand participants' last public appearance until their hand ends.
            var accounts = new Dictionary<string, PlayerAccount>();
            foreach (var name in room.MemberNames)
            {
                if (!_playerManager.TryGetAccount(name, out var account)) continue;
                accounts[name] = account;
                room.Namecards[name] = account.EquippedNamecardId ?? string.Empty;
            }
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository as IServiceYearsRepository;
            var years = repository?.ReadServiceYears(accounts.Values.Select(account => account.AccountId).ToArray());
            foreach (var entry in accounts)
                room.ServiceYears[entry.Key] = years != null && years.TryGetValue(entry.Value.AccountId, out var total)
                    ? total : 0UL;
            return room;
        }
    }
}
