using System.Collections.Generic;

namespace Game.Network.Accounts
{
    public interface IServiceYearsRepository
    {
        // Server-only account IDs in; only the public total is included in client room snapshots.
        Dictionary<string, ulong> ReadServiceYears(string[] accountIds);
    }
}