#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System;
using Microsoft.Data.Sqlite;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository
    {
        private static uint ReadInHandTaskRewards(SqliteConnection connection, SqliteTransaction transaction,
            string handId, string accountId)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"SELECT COALESCE(SUM(amount),0) FROM chip_ledger
WHERE hand_id=$hand AND account_id=$account AND transaction_type='TaskReward';";
            command.Parameters.AddWithValue("$hand", handId);
            command.Parameters.AddWithValue("$account", accountId);
            return checked((uint)Convert.ToInt64(command.ExecuteScalar()));
        }
    }
}
#endif
