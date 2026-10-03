#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository
    {
        private static AchievementDocument ReadAchievementDocument(string json)
        {
            var result = JsonSerializer.Deserialize<AchievementDocument>(json) ?? throw new InvalidOperationException("Invalid achievement document.");
            using var document = JsonDocument.Parse(json);
            foreach (var old in document.RootElement.GetProperty("Achievements").EnumerateArray())
            {
                if (old.TryGetProperty("Reward", out _)) continue;
                var state = result.Achievements.Find(a => a.Id == old.GetProperty("Id").GetString());
                state.Reward = new Reward(old.TryGetProperty("RewardChips", out var dollars) ? dollars.GetUInt32() : 0,
                    old.TryGetProperty("RewardItems", out var items) && items.ValueKind == JsonValueKind.Array
                        ? items.EnumerateArray().Select(i => new ItemStack(i.GetProperty("ItemId").GetString(), i.GetProperty("Quantity").GetUInt32())) : null);
            }
            return result;
        }

        // Schema 16: assigned tasks keep only their task ID. Rewriting each document drops the stored definition JSON,
        // configuration revision, display order and item limit snapshots, and converts receipts from before unified rewards.
        private static void MigrateTaskDocuments(SqliteConnection connection, SqliteTransaction transaction)
        {
            var documents = new List<(string Account, string Json)>();
            using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "SELECT account_id, document FROM task_accounts;";
                using var reader = read.ExecuteReader();
                while (reader.Read()) documents.Add((reader.GetString(0), reader.GetString(1)));
            }
            using var write = connection.CreateCommand();
            write.Transaction = transaction;
            write.CommandText = "UPDATE task_accounts SET document=$json WHERE account_id=$account;";
            var account = write.Parameters.Add("$account", SqliteType.Text);
            var json = write.Parameters.Add("$json", SqliteType.Text);
            foreach (var document in documents)
            {
                account.Value = document.Account;
                json.Value = JsonSerializer.Serialize(ReadLegacyTaskDocument(document.Json));
                write.ExecuteNonQuery();
            }
        }

        private static PlayerTaskDocument ReadLegacyTaskDocument(string json)
        {
            var result = JsonSerializer.Deserialize<PlayerTaskDocument>(json) ?? throw new InvalidOperationException("Invalid task account document.");
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("Receipts", out var receipts)) return result;
            foreach (var old in receipts.EnumerateArray())
            {
                if (old.TryGetProperty("Reward", out _)) continue;
                var receipt = result.Receipts.Find(r => r.InstanceId == old.GetProperty("InstanceId").GetString() && r.Ordinal == old.GetProperty("Ordinal").GetInt32());
                receipt.Reward = new Reward(old.TryGetProperty("Chip", out var dollars) ? dollars.GetUInt32() : 0,
                    old.TryGetProperty("Items", out var items) && items.ValueKind == JsonValueKind.Array
                        ? items.EnumerateArray().Select(i => new ItemStack(i.GetProperty("ItemId").GetString(), i.GetProperty("Count").GetUInt32())) : null);
            }
            return result;
        }
    }
}
#endif
