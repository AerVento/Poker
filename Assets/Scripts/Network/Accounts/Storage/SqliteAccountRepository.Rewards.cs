#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository
    {
        private enum RewardGrantStatus { Success, BalanceLimit, UnknownItem, ActiveHand }

        // The caller owns the transaction and its durable claim/draw identity.
        private static RewardGrantStatus ApplyReward(SqliteConnection connection, SqliteTransaction transaction,
            PlayerAccount account, Reward reward, IReadOnlyDictionary<string, uint> limits, string claimKey,
            string source, out Reward granted)
        {
            granted = Reward.Empty;
            reward ??= Reward.Empty;
            if (reward.Dollars > uint.MaxValue - account.Dollars || reward.Chips > uint.MaxValue - account.Chips)
                return RewardGrantStatus.BalanceLimit;
            if (reward.Chips > 0 && IsAccountInActiveHand(connection, transaction, account.AccountId))
                return RewardGrantStatus.ActiveHand;
            var entries = new List<RewardEntry>();
            var ownedItems = ReadItemStacks(connection, transaction, account.AccountId).ToDictionary(i => i.ItemId, i => i.Quantity);
            foreach (var entry in reward.Entries)
            {
                if (entry.Kind != RewardItemKind.Item) { entries.Add(entry); continue; }
                if (!limits.TryGetValue(entry.ItemId, out var maximum)) return RewardGrantStatus.UnknownItem;
                ownedItems.TryGetValue(entry.ItemId, out var owned);
                var quantity = Math.Min(entry.Quantity, owned >= maximum ? 0UL : maximum - owned);
                if (quantity > 0) entries.Add(new RewardEntry { Kind = entry.Kind, ItemId = entry.ItemId, Quantity = quantity });
            }
            granted = new Reward(entries);
            var now = DateTime.UtcNow;
            var dollars = checked(account.Dollars + (uint)granted.Dollars);
            var chips = checked(account.Chips + (uint)granted.Chips);
            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = @"UPDATE player_accounts SET dollars=$dollars,chips=$chips,version=version+1,updated_at_utc=$now
WHERE account_id=$account AND version=$version;";
                update.Parameters.AddWithValue("$dollars", (long)dollars);
                update.Parameters.AddWithValue("$chips", (long)chips);
                update.Parameters.AddWithValue("$now", FormatDateTime(now));
                update.Parameters.AddWithValue("$account", account.AccountId);
                update.Parameters.AddWithValue("$version", account.Version);
                if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Reward account version changed.");
            }
            foreach (var entry in granted.Entries.Where(e => e.Kind == RewardItemKind.Item))
                AddItems(connection, transaction, account.AccountId, entry.ItemId, (uint)entry.Quantity);
            if (granted.Dollars > 0) InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"), claimKey,
                account.AccountId, source, (long)granted.Dollars, account.Dollars, dollars, null, null, now);
            if (granted.Chips > 0) InsertLedger(connection, transaction, Guid.NewGuid().ToString("N"), claimKey,
                account.AccountId, source, (long)granted.Chips, account.Chips, chips, null, null, now);
            if (granted.Items.Count > 0)
                RecordInventoryChanged(connection, transaction, account.AccountId, claimKey + ":inventory", source, now);
            else RecordAssetsChanged(connection, transaction, account.AccountId, claimKey + ":assets", source, now);
            account.Dollars = dollars; account.Chips = chips; account.Version++;
            return RewardGrantStatus.Success;
        }

        /// <summary>Adds to an owned stack; callers have already capped the quantity at the item's limit.</summary>
        private static void AddItems(SqliteConnection connection, SqliteTransaction transaction, string accountId, string itemId, uint quantity)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = @"INSERT INTO account_items(account_id,item_id,quantity) VALUES($account,$item,$quantity)
ON CONFLICT(account_id,item_id) DO UPDATE SET quantity=quantity+excluded.quantity;";
            update.Parameters.AddWithValue("$account", accountId);
            update.Parameters.AddWithValue("$item", itemId);
            update.Parameters.AddWithValue("$quantity", (long)quantity);
            update.ExecuteNonQuery();
        }
    }
}
#endif
