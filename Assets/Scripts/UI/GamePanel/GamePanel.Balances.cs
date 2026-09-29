using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network;

namespace Game.UI
{
    public partial class GamePanel
    {
        private RoundResult _pendingBalanceResult;
        private string _playedBalanceHand;
        private readonly Dictionary<string, uint> _heldChips = new Dictionary<string, uint>();
        private readonly Dictionary<string, ulong> _beforeBets = new Dictionary<string, ulong>();
        private readonly Dictionary<string, uint> _roundBets = new Dictionary<string, uint>();

        public void HoldRoundBalances(RoundResult result)
        {
            if (result == null || result.HandId == _playedBalanceHand ||
                _info.HandId != result.HandId || _info.RoomInfo?.Id != result.RoomId) return;
            if (_pendingBalanceResult?.HandId == result.HandId) return;
            _pendingBalanceResult = result;
            _heldChips.Clear();
            foreach (var player in _info.Players)
            {
                if (!player.IsOccupied) continue;
                _roundBets[player.PlayerName] = player.RoundBet;
                var award = result.PlayerResults.FirstOrDefault(p => p.PlayerName == player.PlayerName)?.ChipEarned ?? 0;
                _heldChips[player.PlayerName] = _info.Round >= GameStage.RoundOver
                    ? (uint)Math.Max(0L, (long)player.Chips - award) : player.Chips;
            }
        }

        private void ObserveBalanceSnapshot(ClientGameInfo next)
        {
            if (_info.HandId != next.HandId || _info.RoomInfo?.Id != next.RoomInfo?.Id)
            {
                _pendingBalanceResult = null;
                _heldChips.Clear();
                _roundBets.Clear();
                _beforeBets.Clear();
            }
            if (next.Players == null) return;
            foreach (var player in next.Players)
            {
                if (!player.IsOccupied) continue;
                _roundBets[player.PlayerName] = player.RoundBet;
                if (next.Round < GameStage.RoundOver)
                    _beforeBets[player.PlayerName] = (ulong)player.Chips + player.RoundBet;
                if (_pendingBalanceResult == null || next.Round < GameStage.RoundOver) continue;
                var award = _pendingBalanceResult.PlayerResults.FirstOrDefault(p => p.PlayerName == player.PlayerName)?.ChipEarned ?? 0;
                // Capture the last action as well, even if the result arrived before its final snapshot.
                _heldChips[player.PlayerName] = _beforeBets.TryGetValue(player.PlayerName, out var before)
                    ? (uint)Math.Max(0L, (long)before - player.RoundBet)
                    : (uint)Math.Max(0L, (long)player.Chips - award);
            }
        }

        private uint PresentedChips(ClientGameInfo.PlayerInfo player)
            => _pendingBalanceResult != null && _heldChips.TryGetValue(player.PlayerName, out var held)
                ? held : player.Chips;

        public void ReleaseRoundBalances(RoundResult result)
        {
            if (!isActiveAndEnabled || result == null || _pendingBalanceResult?.HandId != result.HandId ||
                _info.HandId != result.HandId || _info.RoomInfo?.Id != result.RoomId) return;
            _playedBalanceHand = result.HandId;
            _pendingBalanceResult = null;
            if (_playerListItems == null) return;
            foreach (var item in _playerListItems)
            {
                if (!item.gameObject.activeInHierarchy) continue;
                var entry = result.PlayerResults.FirstOrDefault(p => p.PlayerName == item.PlayerName);
                if (entry == null) continue;
                var player = _info.Players.FirstOrDefault(p => p.IsOccupied && p.PlayerName == entry.PlayerName);
                if (player.PlayerName == null) continue;
                uint balance = player.Chips;
                if (_info.Round < GameStage.RoundOver && _heldChips.TryGetValue(entry.PlayerName, out var held))
                    balance = (uint)Math.Min(uint.MaxValue, (ulong)held + entry.ChipEarned);
                item.Chip = balance;
                if (_beforeBets.TryGetValue(entry.PlayerName, out var before))
                    item.ShowNetResult((long)balance - (long)before);
                else if (_roundBets.TryGetValue(entry.PlayerName, out var bet))
                    item.ShowNetResult((long)entry.ChipEarned - bet);
            }
            _heldChips.Clear();
        }

        private void ResetBalancePresentation()
        {
            _pendingBalanceResult = null;
            _playedBalanceHand = null;
            _heldChips.Clear();
            _roundBets.Clear();
            _beforeBets.Clear();
        }
    }
}
