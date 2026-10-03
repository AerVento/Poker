#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : ISlotLotteryRepository
    {
        private static void CreateSlotLotterySchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"CREATE TABLE slot_draw_receipts (
account_id TEXT NOT NULL REFERENCES player_accounts(account_id), operation_id TEXT NOT NULL,
receipt_json TEXT NOT NULL, acknowledged INTEGER NOT NULL DEFAULT 0, created_at_utc TEXT NOT NULL,
PRIMARY KEY(account_id,operation_id));
CREATE INDEX ix_slot_pending ON slot_draw_receipts(account_id,acknowledged,created_at_utc);";
            command.ExecuteNonQuery();
        }

        private static void CreateSlotPitySchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"CREATE TABLE IF NOT EXISTS slot_pity (
account_id TEXT NOT NULL PRIMARY KEY REFERENCES player_accounts(account_id), misses INTEGER NOT NULL DEFAULT 0);";
            command.ExecuteNonQuery();
        }

        private static int ReadSlotMisses(SqliteConnection connection, SqliteTransaction transaction, string accountId)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT misses FROM slot_pity WHERE account_id=$account;";
            command.Parameters.AddWithValue("$account", accountId);
            return command.ExecuteScalar() is long misses ? (int)misses : 0;
        }

        private static void WriteSlotMisses(SqliteConnection connection, SqliteTransaction transaction, string accountId, int misses)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"INSERT INTO slot_pity(account_id,misses) VALUES($account,$misses)
ON CONFLICT(account_id) DO UPDATE SET misses=excluded.misses;";
            command.Parameters.AddWithValue("$account", accountId);
            command.Parameters.AddWithValue("$misses", misses);
            command.ExecuteNonQuery();
        }

        public int ReadSlotMisses(string accountId)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                return ReadSlotMisses(connection, null, accountId);
            }
        }

        private static SlotDrawReceipt ReadSlotReceipt(SqliteConnection connection, SqliteTransaction transaction,
            string accountId, string operationId = null)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT receipt_json FROM slot_draw_receipts WHERE account_id=$account AND " +
                (operationId == null ? "acknowledged=0 ORDER BY created_at_utc LIMIT 1;" : "operation_id=$operation;");
            command.Parameters.AddWithValue("$account", accountId);
            if (operationId != null) command.Parameters.AddWithValue("$operation", operationId);
            var json = command.ExecuteScalar() as string;
            return json == null ? null : JsonSerializer.Deserialize<SlotDrawReceipt>(json);
        }

        public SlotDrawReceipt ReadPendingSlot(string accountId)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                return ReadSlotReceipt(connection, null, accountId);
            }
        }

        public void AcknowledgeSlot(string accountId, string operationId)
        {
            if (!Guid.TryParseExact(operationId, "N", out _)) return;
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE slot_draw_receipts SET acknowledged=1 WHERE account_id=$account AND operation_id=$operation;";
                command.Parameters.AddWithValue("$account", accountId);
                command.Parameters.AddWithValue("$operation", operationId);
                command.ExecuteNonQuery();
            }
        }

        public SlotDrawExecution DrawSlot(string accountId, string operationId, string revision,
            SlotLotteryCatalog catalog, GameCatalog items, int count = 1)
        {
            var execution = new SlotDrawExecution { Result = SlotLotteryResult.InvalidRequest };
            if ((count != 1 && count != 10) || !Guid.TryParseExact(operationId, "N", out _) || string.IsNullOrEmpty(accountId)) return execution;
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                // Receipts survive config changes, lost replies and reconnects. An unseen result must be shown first.
                var prior = ReadSlotReceipt(connection, transaction, accountId, operationId);
                if (prior != null && prior.DrawCount != count) return execution;
                prior ??= ReadSlotReceipt(connection, transaction, accountId);
                if (prior != null) return new SlotDrawExecution { Result = SlotLotteryResult.Success, Receipt = prior };
                if (catalog == null || items == null || !catalog.Enabled)
                    return new SlotDrawExecution { Result = SlotLotteryResult.Unavailable };
                if (revision != catalog.Revision) return new SlotDrawExecution { Result = SlotLotteryResult.ConfigurationChanged };
                var account = FindById(connection, transaction, accountId);
                if (account == null) return execution;
                ulong totalCost = (ulong)catalog.CostDollars * (uint)count;
                if (totalCost > uint.MaxValue) return new SlotDrawExecution { Result = SlotLotteryResult.InsufficientFunds };
                var payment = PrepareDollarPayment(connection, transaction, account, (uint)totalCost, true, 0, "slot:" + operationId);
                if (payment != WalletResult.Success)
                    return new SlotDrawExecution { Result = SlotLotteryResult.InsufficientFunds };
                var beforeCost = account.Dollars;
                account.Dollars -= (uint)totalCost;
                string key = "slot:" + accountId + ":" + operationId;
                var limits = items.Items.ToDictionary(i => i.Id, i => i.MaxQuantity, StringComparer.Ordinal);
                ApplyReward(connection, transaction, account, Reward.Empty, limits, key + ":cost", "SlotDrawCost", out _);
                InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"), key + ":cost", accountId,
                    "SlotDrawCost", -(long)totalCost, beforeCost, account.Dollars, null, null, DateTime.UtcNow);
                var results = new SlotDrawOutcome[count];
                // Consecutive misses carry across single and ten draws; a win resets the count.
                int misses = ReadSlotMisses(connection, transaction, accountId);
                var highestTier = SlotPrizeTier.None;
                for (int i = 0; i < count; i++)
                {
                    // Re-read every draw: an earlier draw in the batch may have filled a namecard or card face slot.
                    var owned = ReadItemStacks(connection, transaction, accountId).ToDictionary(s => s.ItemId, s => s.Quantity, StringComparer.Ordinal);
                    bool Fits(Reward candidate) => SlotLotteryCatalog.Fits(candidate, owned, limits);
                    int winningWeight = catalog.WinningWeight(Fits);
                    var tier = catalog.IsGuaranteed(misses) && winningWeight > 0
                        ? catalog.SelectWinningTier(RandomNumberGenerator.GetInt32(winningWeight), Fits)
                        : catalog.SelectTier(RandomNumberGenerator.GetInt32(catalog.TotalWeight));
                    int eligibleWeight = catalog.EligibleWeight(tier, Fits);
                    // A tier with nothing left to give counts as no prize, matching the per-player disclosure.
                    if (tier != SlotPrizeTier.None && eligibleWeight == 0) tier = SlotPrizeTier.None;
                    misses = tier == SlotPrizeTier.None ? Math.Min(misses + 1, 1000) : 0;
                    var reward = tier == SlotPrizeTier.None ? null :
                        catalog.SelectReward(tier, RandomNumberGenerator.GetInt32(eligibleWeight), Fits).Reward;
                    Reward granted = null;
                    if (reward != null && ApplyReward(connection, transaction, account, reward, limits,
                        key + ":reward:" + i, "SlotDrawReward", out granted) != RewardGrantStatus.Success)
                        return new SlotDrawExecution { Result = SlotLotteryResult.RewardLimit };
                    results[i] = new SlotDrawOutcome { Tier = tier, Reward = granted };
                    if (tier > highestTier) highestTier = tier;
                }
                WriteSlotMisses(connection, transaction, accountId, misses);
                var now = DateTime.UtcNow;
                var receipt = new SlotDrawReceipt { OperationId = operationId, CostDollars = (uint)totalCost, Tier = highestTier, Reward = count == 1 ? results[0].Reward : null, Results = results };
                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO slot_draw_receipts(account_id,operation_id,receipt_json,created_at_utc) VALUES($account,$operation,$json,$now);";
                    insert.Parameters.AddWithValue("$account", accountId);
                    insert.Parameters.AddWithValue("$operation", operationId);
                    insert.Parameters.AddWithValue("$json", JsonSerializer.Serialize(receipt));
                    insert.Parameters.AddWithValue("$now", FormatDateTime(now));
                    insert.ExecuteNonQuery();
                }
                transaction.Commit();
                return new SlotDrawExecution { Result = SlotLotteryResult.Success, Receipt = receipt, NewlyApplied = true };
            }
        }

    }
}
#endif
