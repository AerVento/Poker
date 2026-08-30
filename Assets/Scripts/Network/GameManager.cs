using Cysharp.Threading.Tasks;
using Framework.Log;
using Game.Network.Accounts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Network
{
    /// <summary>
    /// 服务端权威的无限注德州扑克规则与牌局状态管理器。
    /// </summary>
    public class GameManager
    {
        private readonly Dictionary<int, ServerGameInfo> _games = new Dictionary<int, ServerGameInfo>();
        private readonly HashSet<int> _autoRunningRooms = new HashSet<int>();
        private IHandAccountRepository _accountRepository;

        public event Action<int> OnGameInfoChanged;
        public event Action<int> OnGameStageEnded;
        public event Action<int, RoundResult> OnRoomRoundEnded;
        public event Action<int, GameResult> OnRoomGameOver;

        public GameManager(IHandAccountRepository accountRepository = null)
        {
            _accountRepository = accountRepository;
        }

        public void SetAccountRepository(IHandAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }

        public int CreateGame(RoomInfo room)
        {
            if (room == null)
                throw new ArgumentNullException(nameof(room));
            room.EnsureSeatModel();
            if (room.PlayerCount < 2)
                throw new ArgumentException("At least two players are required.", nameof(room));
            if (room.SmallBlindChips <= 0 || room.ThinkingTime <= 0)
                throw new ArgumentException("Blinds and thinking time must be positive.", nameof(room));
            if (_accountRepository == null)
                return -7;

            var accounts = new List<PlayerAccount>(room.PlayerCount);
            var bigBlind = checked((uint)room.SmallBlindChips * 2U);
            var eligiblePlayerCount = 0;
            try
            {
                foreach (var playerName in room.Players)
                {
                    var account = _accountRepository.FindByUserName(playerName);
                    if (account == null)
                        return -7;
                    if (account.Chips >= bigBlind)
                        eligiblePlayerCount++;
                    accounts.Add(account);
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to load account balances for room {room.Id}: {exception}");
                return -7;
            }
            if (eligiblePlayerCount < 2)
                return -8;

            var gameInfo = ServerGameInfo.Create(room, accounts);
            _games.Add(room.Id, gameInfo);
            gameInfo.Timer.OnTimeUp += () => AutoOperation(gameInfo);
            return 0;
        }

        public ServerGameInfo GetGameInfo(int roomId)
        {
            return _games.TryGetValue(roomId, out var info) ? info : null;
        }

        private static uint GetBigBlindAmount(ServerGameInfo info)
        {
            if (info.RoomInfo.SmallBlindChips <= 0)
                throw new InvalidOperationException("The small blind must be positive.");

            return checked((uint)info.RoomInfo.SmallBlindChips * 2U);
        }

        private static bool CanAct(ServerGameInfo.PlayerInfo player)
        {
            return player.IsOccupied &&
                player.ParticipatedThisHand &&
                !player.IsFold &&
                !player.IsAllIn;
        }

        private static bool CanParticipateInNextHand(
            ServerGameInfo info,
            ServerGameInfo.PlayerInfo player)
        {
            return player.IsOccupied &&
                !player.IsLeaving &&
                player.Chips >= GetBigBlindAmount(info) &&
                !player.IsDisconnected;
        }

        private static int FindSeatAtOrAfter(
            ServerGameInfo info,
            int firstSeat,
            Func<ServerGameInfo.PlayerInfo, bool> predicate)
        {
            for (int offset = 0; offset < info.PlayersCount; offset++)
            {
                var candidate = (firstSeat + offset) % info.PlayersCount;
                if (predicate(info.Players[candidate]))
                    return candidate;
            }

            return -1;
        }

        private static int FindNextSeat(
            ServerGameInfo info,
            int currentSeat,
            Func<ServerGameInfo.PlayerInfo, bool> predicate)
        {
            return FindSeatAtOrAfter(info, (currentSeat + 1) % info.PlayersCount, predicate);
        }

        private static bool IsBettingRoundComplete(ServerGameInfo info)
        {
            if (info.Players.Count(player => !player.IsFold) <= 1)
                return true;

            var actionablePlayers = info.Players.Where(CanAct).ToList();
            if (actionablePlayers.Count == 0)
                return true;

            if (actionablePlayers.Count == 1 &&
                actionablePlayers[0].StageBet >= info.CurrentHighestPlayerBet)
            {
                return true;
            }

            return actionablePlayers.All(player =>
                player.HasActedThisStage &&
                player.StageBet == info.CurrentHighestPlayerBet);
        }

        private static bool CanRunOutBoard(ServerGameInfo info)
        {
            var actionablePlayers = info.Players.Where(CanAct).ToList();
            if (actionablePlayers.Count == 0)
                return true;

            return actionablePlayers.Count == 1 &&
                actionablePlayers[0].StageBet >= info.CurrentHighestPlayerBet;
        }

        private static PotCalculator.Result CalculatePots(ServerGameInfo info)
        {
            return PotCalculator.Calculate(
                info.Players.Select(player => player.RoundBet).ToArray(),
                info.Players.Select(player => player.IsFold).ToArray());
        }

        private bool ApplySettlement(
            ServerGameInfo info,
            IReadOnlyList<uint> earned,
            PotCalculator.Result calculation)
        {
            var credits = new List<HandChipCredit>();
            ulong totalCredit = 0;
            for (int playerIndex = 0; playerIndex < calculation.Refunds.Length; playerIndex++)
            {
                var credit = checked(calculation.Refunds[playerIndex] + earned[playerIndex]);
                totalCredit = checked(totalCredit + credit);
                if (credit == 0)
                    continue;

                credits.Add(new HandChipCredit
                {
                    AccountId = info.Players[playerIndex].AccountId,
                    Amount = credit,
                });
            }

            if (totalCredit != info.TotalPot)
                throw new InvalidOperationException("The hand settlement does not conserve the pot.");

            HandSettlementResult result;
            IReadOnlyDictionary<string, PlayerAccount> accounts;
            try
            {
                result = _accountRepository.SettleHand(
                    info.HandId,
                    info.HandId + ":settlement",
                    credits,
                    out accounts);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to settle hand {info.HandId}: {exception}");
                return false;
            }
            if (result != HandSettlementResult.Applied && result != HandSettlementResult.AlreadyApplied)
                return false;

            foreach (var player in info.Players)
            {
                if (player.IsOccupied &&
                    !string.IsNullOrEmpty(player.AccountId) &&
                    accounts.TryGetValue(player.AccountId, out var account))
                {
                    player.Chips = account.Chips;
                }
            }

            info.TotalPot = 0;
            return true;
        }

        private bool CommitChips(ServerGameInfo info, ServerGameInfo.PlayerInfo player, uint chipCount)
        {
            if (chipCount > player.Chips)
                throw new InvalidOperationException("A player cannot commit more chips than are in their stack.");

            var requestId = info.HandId + ":action:" + info.ActionSequence.ToString();
            HandDebitResult result;
            IReadOnlyDictionary<string, PlayerAccount> accounts;
            try
            {
                result = _accountRepository.ApplyHandDebits(
                    info.HandId,
                    requestId,
                    new[]
                    {
                        new HandChipDebit { AccountId = player.AccountId, Amount = chipCount },
                    },
                    out accounts);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to persist an action for hand {info.HandId}: {exception}");
                return false;
            }
            if ((result != HandDebitResult.Applied && result != HandDebitResult.AlreadyApplied) ||
                !accounts.TryGetValue(player.AccountId, out var account))
                return false;

            info.ActionSequence++;
            player.Chips = account.Chips;
            player.StageBet = checked(player.StageBet + chipCount);
            player.RoundBet = checked(player.RoundBet + chipCount);
            info.TotalPot = checked(info.TotalPot + chipCount);
            if (player.Chips == 0)
                player.IsAllIn = true;
            return true;
        }

        private bool PostBlinds(ServerGameInfo info, uint smallBlindAmount, uint bigBlindAmount)
        {
            var smallBlind = info.Players[info.SmallBlind];
            var bigBlind = info.Players[info.BigBlind];
            var smallCommit = Math.Min(smallBlind.Chips, smallBlindAmount);
            var bigCommit = Math.Min(bigBlind.Chips, bigBlindAmount);
            HandDebitResult result;
            IReadOnlyDictionary<string, PlayerAccount> accounts;
            try
            {
                result = _accountRepository.ApplyHandDebits(
                    info.HandId,
                    info.HandId + ":blinds",
                    new[]
                    {
                        new HandChipDebit { AccountId = smallBlind.AccountId, Amount = smallCommit },
                        new HandChipDebit { AccountId = bigBlind.AccountId, Amount = bigCommit },
                    },
                    out accounts);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to persist blinds for hand {info.HandId}: {exception}");
                return false;
            }
            if (result != HandDebitResult.Applied && result != HandDebitResult.AlreadyApplied)
                return false;

            ApplyCommittedBlind(info, smallBlind, smallCommit, accounts[smallBlind.AccountId]);
            ApplyCommittedBlind(info, bigBlind, bigCommit, accounts[bigBlind.AccountId]);
            return true;
        }

        private static void ApplyCommittedBlind(
            ServerGameInfo info,
            ServerGameInfo.PlayerInfo player,
            uint chipCount,
            PlayerAccount account)
        {
            player.Chips = account.Chips;
            player.StageBet = checked(player.StageBet + chipCount);
            player.RoundBet = checked(player.RoundBet + chipCount);
            info.TotalPot = checked(info.TotalPot + chipCount);
            if (player.Chips == 0)
                player.IsAllIn = true;
        }

        private static void ResetActionTimer(ServerGameInfo info)
        {
            if (info.Timer.IsCounting)
                info.Timer.Clear();
            else
                info.Timer.Start();
        }

        private void AdvanceAfterAction(ServerGameInfo info)
        {
            ResetActionTimer(info);

            if (IsBettingRoundComplete(info))
            {
                info.OperatingPlayer = -1;
                OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
                OnGameStageEnded?.Invoke(info.RoomInfo.Id);
                return;
            }

            var nextPlayer = FindNextSeat(info, info.OperatingPlayer, CanAct);
            if (nextPlayer < 0)
                throw new InvalidOperationException("The betting round is incomplete but no player can act.");

            info.OperatingPlayer = nextPlayer;
            if (info.Players[nextPlayer].IsDisconnected)
                AutoOperation(info);
            else
                OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
        }

        private static void BurnCard(ServerGameInfo info)
        {
            if (info.Deck == null || info.Deck.Count == 0)
                throw new InvalidOperationException("The deck has no card available to burn.");

            info.Deck.Dequeue();
        }

        private static void DealCommunityCards(ServerGameInfo info, int cardCount)
        {
            BurnCard(info);
            for (int i = 0; i < cardCount; i++)
            {
                if (info.Deck.Count == 0)
                    throw new InvalidOperationException("The deck has no community card available.");
                info.Community.Add(info.Deck.Dequeue());
            }
        }

        private void BeginNextStreet(ServerGameInfo info, GameStage nextStage, int communityCardCount)
        {
            info.Stage = nextStage;
            DealCommunityCards(info, communityCardCount);
            info.CurrentHighestPlayerBet = 0;
            info.CurrentHighestRaise = GetBigBlindAmount(info);

            foreach (var player in info.Players)
            {
                player.StageBet = 0;
                player.LastOperation = null;
                player.HasActedThisStage = false;
                player.BetFacedAtLastAction = 0;
            }

            info.OperatingPlayer = FindNextSeat(info, info.Dealer, CanAct);
            if (info.OperatingPlayer < 0)
                throw new InvalidOperationException("A betting street was started without a player who can act.");

            ResetActionTimer(info);
            if (info.Players[info.OperatingPlayer].IsDisconnected)
                AutoOperation(info);
            else
                OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
        }

        private void DealRunoutStreet(ServerGameInfo info, GameStage nextStage, int communityCardCount)
        {
            info.Stage = nextStage;
            DealCommunityCards(info, communityCardCount);
            info.CurrentHighestPlayerBet = 0;
            info.CurrentHighestRaise = GetBigBlindAmount(info);
            info.OperatingPlayer = -1;

            foreach (var player in info.Players)
            {
                player.StageBet = 0;
                player.LastOperation = null;
                player.HasActedThisStage = false;
                player.BetFacedAtLastAction = 0;
            }

            OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
        }

        private void CompleteRound(
            ServerGameInfo info,
            IReadOnlyList<uint> earned,
            bool revealShowdownHands)
        {
            info.OperatingPlayer = -1;
            info.TotalPot = 0;
            info.Timer.Stop();

            info.Stage = GameStage.RoundOver;

            var roundResult = new RoundResult() { Community = info.Community };
            for (int i = 0; i < info.PlayersCount; i++)
            {
                if (!info.Players[i].ParticipatedThisHand)
                    continue;

                roundResult.PlayerResults.Add(new RoundResult.Player()
                {
                    PlayerName = info.Players[i].PlayerName,
                    Hole = revealShowdownHands && !info.Players[i].IsFold
                        ? info.Players[i].Hole
                        : new List<Poker>(),
                    IsFold = info.Players[i].IsFold,
                    IsDisconnected = info.Players[i].IsDisconnected,
                    ChipEarned = earned[i],
                });
            }

            UpdateRoomSeatStatesAfterHand(info);
            OnRoomRoundEnded?.Invoke(info.RoomInfo.Id, roundResult);
        }

        private bool RiverExiting(ServerGameInfo info)
        {
            info.OperatingPlayer = -1;
            var potCalculation = CalculatePots(info);

            var patterns = new List<(int Player, PokerPattern Pattern)>();
            for (int playerIndex = 0; playerIndex < info.PlayersCount; playerIndex++)
            {
                if (info.Players[playerIndex].IsFold)
                    continue;

                var cards = info.Community.Concat(info.Players[playerIndex].Hole).ToList();
                patterns.Add((playerIndex, PokerUtility.GetBiggestPattern(cards, out _)));
            }

            patterns.Sort((left, right) =>
            {
                if (left.Pattern.BiggerThan(right.Pattern))
                    return -1;
                if (left.Pattern.SmallerThan(right.Pattern))
                    return 1;
                return left.Player.CompareTo(right.Player);
            });

            var earned = new uint[info.PlayersCount];
            foreach (var pot in potCalculation.Pots)
            {
                var eligiblePatterns = patterns
                    .Where(pattern => pot.EligiblePlayers.Contains(pattern.Player))
                    .ToList();
                if (eligiblePatterns.Count == 0)
                    throw new InvalidOperationException("A pot has no eligible player at showdown.");

                var bestPattern = eligiblePatterns[0].Pattern;
                var winners = eligiblePatterns
                    .TakeWhile(pattern => pattern.Pattern.SameTo(bestPattern))
                    .Select(pattern => pattern.Player)
                    .ToList();

                var winnerCount = checked((uint)winners.Count);
                var share = pot.ChipCount / winnerCount;
                foreach (var winner in winners)
                {
                    earned[winner] = checked(earned[winner] + share);
                }

                var oddChips = pot.ChipCount % winnerCount;
                var oddChipOrder = PotCalculator.GetOddChipOrder(winners, info.Dealer, info.PlayersCount);
                for (int offset = 0; offset < oddChips; offset++)
                {
                    var winner = oddChipOrder[offset];
                    earned[winner] = checked(earned[winner] + 1);
                }
            }

            if (!ApplySettlement(info, earned, potCalculation))
                return false;

            CompleteRound(info, earned, revealShowdownHands: true);
            return true;
        }

        private bool EarlyTermination(ServerGameInfo info)
        {
            var potCalculation = CalculatePots(info);
            var earned = new uint[info.PlayersCount];

            foreach (var pot in potCalculation.Pots)
            {
                if (pot.EligiblePlayers.Count != 1)
                    throw new InvalidOperationException("An uncontested pot must have exactly one eligible player.");

                var winner = pot.EligiblePlayers[0];
                earned[winner] = checked(earned[winner] + pot.ChipCount);
            }

            if (!ApplySettlement(info, earned, potCalculation))
                return false;

            CompleteRound(info, earned, revealShowdownHands: false);
            return true;
        }

        private async void Auto(ServerGameInfo info)
        {
            var roomId = info.RoomInfo.Id;
            if (!_autoRunningRooms.Add(roomId))
                return;

            info.OperatingPlayer = -1;
            info.Timer.Stop();
            try
            {
                while (_games.TryGetValue(roomId, out var currentInfo) &&
                    ReferenceEquals(currentInfo, info) &&
                    info.Stage != GameStage.RoundOver &&
                    info.Stage != GameStage.GameOver)
                {
                    if (info.Players.Count(player => !player.IsFold) <= 1)
                    {
                        if (!EarlyTermination(info))
                            Log.WriteError($"Failed to persist the settlement for room {roomId}.");
                        break;
                    }

                    switch (info.Stage)
                    {
                        case GameStage.Preflop:
                            DealRunoutStreet(info, GameStage.Flop, 3);
                            break;
                        case GameStage.Flop:
                            DealRunoutStreet(info, GameStage.Turn, 1);
                            break;
                        case GameStage.Turn:
                            DealRunoutStreet(info, GameStage.River, 1);
                            break;
                        case GameStage.River:
                            if (!RiverExiting(info))
                                Log.WriteError($"Failed to persist the settlement for room {roomId}.");
                            return;
                    }

                    await UniTask.WaitForSeconds(3);
                }
            }
            finally
            {
                _autoRunningRooms.Remove(roomId);
            }
        }

        public bool IsHandInProgress(int roomId)
        {
            return _games.TryGetValue(roomId, out var info) &&
                info.Stage != GameStage.RoundOver &&
                info.Stage != GameStage.GameOver;
        }

        public int FindPlayerSeat(int roomId, string playerName)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return -1;

            return info.Players.FindIndex(player =>
                player.IsOccupied && player.PlayerName == playerName);
        }

        public int CountEligiblePlayers(int roomId)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return 0;

            return info.Players.Count(player => CanParticipateInNextHand(info, player));
        }

        public int SynchronizeRoomSeats(int roomId)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return -1;

            if (!SynchronizeRoomSeats(info))
                return -7;

            return 0;
        }

        public int SetLeaving(int roomId, string playerName)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return -1;

            var playerIndex = FindPlayerSeat(roomId, playerName);
            if (playerIndex < 0)
                return -2;

            var player = info.Players[playerIndex];
            player.IsLeaving = true;
            player.IsWaitingForNextHand = false;
            if (!IsHandInProgress(roomId) || !player.ParticipatedThisHand || player.IsFold || player.IsAllIn)
            {
                OnGameInfoChanged?.Invoke(roomId);
                return 0;
            }

            player.IsFold = true;
            player.LastOperation = new PlayerOperation { Type = PlayerOperationType.Fold };
            player.HasActedThisStage = true;
            player.BetFacedAtLastAction = info.CurrentHighestPlayerBet;
            if (playerIndex == info.OperatingPlayer)
            {
                AdvanceAfterAction(info);
            }
            else if (info.Players.Count(candidate =>
                candidate.ParticipatedThisHand && !candidate.IsFold) <= 1 ||
                IsBettingRoundComplete(info))
            {
                info.OperatingPlayer = -1;
                OnGameInfoChanged?.Invoke(roomId);
                OnGameStageEnded?.Invoke(roomId);
            }
            else
            {
                OnGameInfoChanged?.Invoke(roomId);
            }

            return 0;
        }

        private bool SynchronizeRoomSeats(ServerGameInfo info)
        {
            info.RoomInfo.EnsureSeatModel();
            if (info.RoomInfo.Seats.Any(seat =>
                    seat.SeatIndex < 0 || seat.SeatIndex >= info.PlayersCount) ||
                info.RoomInfo.Seats.Select(seat => seat.SeatIndex).Distinct().Count() !=
                info.RoomInfo.Seats.Count)
            {
                return false;
            }

            try
            {
                for (int seatIndex = 0; seatIndex < info.PlayersCount; seatIndex++)
                {
                    var roomSeat = info.RoomInfo.Seats.Find(seat => seat.SeatIndex == seatIndex);
                    var currentPlayer = info.Players[seatIndex];
                    if (roomSeat == null)
                    {
                        if (!IsHandInProgress(info.RoomInfo.Id) || !currentPlayer.ParticipatedThisHand)
                            info.Players[seatIndex] = CreateEmptySeat(seatIndex);
                        continue;
                    }

                    if (currentPlayer.IsOccupied && currentPlayer.PlayerName == roomSeat.PlayerName)
                    {
                        currentPlayer.IsLeaving = roomSeat.State == RoomSeatState.Leaving;
                        currentPlayer.IsWaitingForNextHand =
                            roomSeat.State == RoomSeatState.WaitingForNextHand ||
                            roomSeat.State == RoomSeatState.Waiting;
                        continue;
                    }

                    if (IsHandInProgress(info.RoomInfo.Id) && currentPlayer.ParticipatedThisHand)
                        return false;

                    var account = _accountRepository.FindByUserName(roomSeat.PlayerName);
                    if (account == null)
                        return false;
                    info.Players[seatIndex] = ServerGameInfo.CreatePlayer(roomSeat, account);
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to synchronize seats for room {info.RoomInfo.Id}: {exception}");
                return false;
            }
        }

        private static ServerGameInfo.PlayerInfo CreateEmptySeat(int seatIndex)
        {
            return new ServerGameInfo.PlayerInfo
            {
                SeatIndex = seatIndex,
                IsFold = true,
            };
        }

        private static void UpdateRoomSeatStatesAfterHand(ServerGameInfo info)
        {
            foreach (var seat in info.RoomInfo.Seats)
            {
                var player = info.Players[seat.SeatIndex];
                if (seat.State == RoomSeatState.Leaving)
                    continue;

                var eligible = CanParticipateInNextHand(info, player);
                seat.State = eligible
                    ? RoomSeatState.WaitingForNextHand
                    : RoomSeatState.Watching;
                player.IsWaitingForNextHand = eligible;
            }

            info.RoomInfo.SynchronizeLegacyLists();
        }

        public int StartNew(int roomId)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return -1;
            if (info.Stage == GameStage.GameOver)
                return -2;
            if (info.RoomInfo.SmallBlindChips <= 0)
                return -3;
            if (!SynchronizeRoomSeats(info))
                return -7;

            var bigBlindAmount = GetBigBlindAmount(info);
            try
            {
                foreach (var player in info.Players)
                {
                    if (!player.IsOccupied || player.IsLeaving)
                        continue;

                    var account = _accountRepository.FindById(player.AccountId);
                    if (account == null)
                        return -7;
                    player.Chips = account.Chips;
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to refresh account balances for room {roomId}: {exception}");
                return -7;
            }

            var participatingPlayers = info.Players
                .Select(player => CanParticipateInNextHand(info, player))
                .ToArray();
            if (participatingPlayers.Count(participates => participates) < 2)
                return -2;

            var handId = Guid.NewGuid().ToString("N");
            var participatingAccountIds = info.Players
                .Where((player, index) => participatingPlayers[index])
                .Select(player => player.AccountId)
                .ToArray();
            HandStartResult handStartResult;
            IReadOnlyList<PlayerAccount> handAccounts;
            try
            {
                handStartResult = _accountRepository.BeginHand(
                    handId,
                    roomId,
                    participatingAccountIds,
                    bigBlindAmount,
                    out handAccounts);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to begin a hand in room {roomId}: {exception}");
                return -7;
            }
            if (handStartResult != HandStartResult.Started)
                return handStartResult == HandStartResult.InsufficientChips ? -2 : -7;

            info.HandId = handId;
            info.ActionSequence = 0;
            var handAccountsById = handAccounts.ToDictionary(account => account.AccountId);
            foreach (var player in info.Players)
            {
                player.ParticipatedThisHand = participatingAccountIds.Contains(player.AccountId);
                player.HandStartingChips = player.ParticipatedThisHand
                    ? handAccountsById[player.AccountId].Chips
                    : 0;
                player.IsWaitingForNextHand = !player.ParticipatedThisHand && player.IsOccupied;
            }

            info.Timer.Stop();
            info.Deck = Poker.RandomDeck();
            info.Community.Clear();
            info.TotalPot = 0;
            info.Stage = GameStage.Preflop;

            var participantCount = participatingPlayers.Count(participates => participates);
            if (participantCount == 2)
            {
                if (info.LastHandParticipantCount > 2 &&
                    info.BigBlind >= 0 &&
                    info.BigBlind < participatingPlayers.Length &&
                    participatingPlayers[info.BigBlind])
                {
                    // 三人及以上转为单挑时，让上一手的大盲成为按钮/小盲，避免另一人连续承担大盲。
                    info.Dealer = info.BigBlind;
                }
                else
                {
                    info.Dealer = HoldemRules.GetNextSeat(info.Dealer, participatingPlayers);
                }
            }
            else
            {
                // 锦标赛使用 dead-button：多人桌按钮每手前进一个物理座位，即使该座位已经淘汰。
                info.Dealer = (info.Dealer + 1) % info.PlayersCount;
            }
            var positions = HoldemRules.GetPositions(info.Dealer, participatingPlayers);
            info.SmallBlind = positions.SmallBlind;
            info.BigBlind = positions.BigBlind;
            info.LastHandParticipantCount = participantCount;

            for (int playerIndex = 0; playerIndex < info.PlayersCount; playerIndex++)
            {
                var player = info.Players[playerIndex];
                player.IsFold = !participatingPlayers[playerIndex];
                player.IsAllIn = false;
                player.StageBet = 0;
                player.RoundBet = 0;
                player.LastOperation = null;
                player.HasActedThisStage = false;
                player.BetFacedAtLastAction = 0;
                player.Hole.Clear();

                var seat = info.RoomInfo.FindSeat(playerIndex);
                if (seat != null && seat.State != RoomSeatState.Leaving)
                {
                    seat.State = participatingPlayers[playerIndex]
                        ? RoomSeatState.Playing
                        : RoomSeatState.Watching;
                }
            }
            info.RoomInfo.SynchronizeLegacyLists();

            var firstDealtPlayer = positions.SmallBlind;
            for (int cardRound = 0; cardRound < 2; cardRound++)
            {
                var playerIndex = firstDealtPlayer;
                for (int dealtPlayerCount = 0; dealtPlayerCount < participantCount; dealtPlayerCount++)
                {
                    info.Players[playerIndex].Hole.Add(info.Deck.Dequeue());
                    playerIndex = HoldemRules.GetNextSeat(playerIndex, participatingPlayers);
                }
            }

            var smallBlindAmount = checked((uint)info.RoomInfo.SmallBlindChips);
            if (!PostBlinds(info, smallBlindAmount, bigBlindAmount))
            {
                SafeAbortHand(info.HandId, "BlindPostingFailed");
                return -7;
            }

            info.CurrentHighestRaise = bigBlindAmount;
            var postedBigBlind = info.Players[info.BigBlind].StageBet;
            info.CurrentHighestPlayerBet = participantCount == 2 && postedBigBlind < bigBlindAmount
                ? Math.Max(postedBigBlind, info.Players[info.SmallBlind].StageBet)
                : bigBlindAmount;

            info.OperatingPlayer = FindSeatAtOrAfter(info, positions.FirstPreflopPlayer, CanAct);
            if (IsBettingRoundComplete(info))
            {
                info.OperatingPlayer = -1;
                OnGameInfoChanged?.Invoke(roomId);
                OnGameStageEnded?.Invoke(roomId);
            }
            else
            {
                if (info.OperatingPlayer < 0)
                    throw new InvalidOperationException("A new hand has no player who can act.");
                info.Timer.Start();
            }

            return 0;
        }

        public int NextStage(int roomId)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return -1;
            if (info.Stage == GameStage.RoundOver || info.Stage == GameStage.GameOver)
                return -2;

            if (info.Players.Count(player => !player.IsFold) <= 1)
            {
                return EarlyTermination(info) ? 0 : -7;
            }

            if (CanRunOutBoard(info))
            {
                Auto(info);
                return 0;
            }

            switch (info.Stage)
            {
                case GameStage.Preflop:
                    BeginNextStreet(info, GameStage.Flop, 3);
                    break;
                case GameStage.Flop:
                    BeginNextStreet(info, GameStage.Turn, 1);
                    break;
                case GameStage.Turn:
                    BeginNextStreet(info, GameStage.River, 1);
                    break;
                case GameStage.River:
                    return RiverExiting(info) ? 0 : -7;
            }

            return 0;
        }

        public void SetDisconnected(int roomId, string playerName)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return;

            var playerIndex = info.Players.FindIndex(player => player.PlayerName == playerName);
            if (playerIndex < 0)
                return;

            info.Players[playerIndex].IsDisconnected = true;
            var roomSeat = info.RoomInfo.FindSeat(playerIndex);
            if (roomSeat != null && roomSeat.State != RoomSeatState.Leaving)
            {
                roomSeat.State = IsHandInProgress(roomId) && info.Players[playerIndex].ParticipatedThisHand
                    ? RoomSeatState.DisconnectedInHand
                    : RoomSeatState.Watching;
                info.RoomInfo.SynchronizeLegacyLists();
            }
            if (playerIndex == info.OperatingPlayer && !info.Players[playerIndex].IsAllIn)
                AutoOperation(info);
            else
                OnGameInfoChanged?.Invoke(roomId);
        }

        public int SetConnected(int roomId, string playerName)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return -1;

            var playerIndex = info.Players.FindIndex(player => player.PlayerName == playerName);
            if (playerIndex < 0)
                return -1;

            info.Players[playerIndex].IsDisconnected = false;
            var roomSeat = info.RoomInfo.FindSeat(playerIndex);
            if (roomSeat != null && roomSeat.State != RoomSeatState.Leaving)
            {
                roomSeat.State = IsHandInProgress(roomId) && info.Players[playerIndex].ParticipatedThisHand
                    ? RoomSeatState.Playing
                    : RoomSeatState.WaitingForNextHand;
                info.RoomInfo.SynchronizeLegacyLists();
            }
            OnGameInfoChanged?.Invoke(roomId);
            return playerIndex;
        }

        private void AutoOperation(ServerGameInfo info)
        {
            var playerIndex = info.OperatingPlayer;
            if (playerIndex < 0 || playerIndex >= info.PlayersCount)
                return;

            var playerName = info.Players[playerIndex].PlayerName;
            if (Check(info.RoomInfo.Id, playerIndex) == 0)
            {
                Log.WriteInfo($"Time up! Player {playerName} in room {info.RoomInfo.Id} checked automatically.");
                return;
            }

            if (Fold(info.RoomInfo.Id, playerIndex) == 0)
                Log.WriteInfo($"Time up! Player {playerName} in room {info.RoomInfo.Id} folded automatically.");
        }

        public int Check(int roomId, int playerIndex)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return -1;
            if (playerIndex < 0 || playerIndex >= info.PlayersCount)
                return -2;
            if (info.OperatingPlayer != playerIndex)
                return -3;

            var player = info.Players[playerIndex];
            if (!CanAct(player) || player.StageBet != info.CurrentHighestPlayerBet)
                return -4;

            player.LastOperation = new PlayerOperation() { Type = PlayerOperationType.Check };
            player.HasActedThisStage = true;
            player.BetFacedAtLastAction = info.CurrentHighestPlayerBet;
            AdvanceAfterAction(info);
            return 0;
        }

        public int Bet(int roomId, int playerIndex, uint num)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return -1;
            if (playerIndex < 0 || playerIndex >= info.PlayersCount)
                return -2;
            if (info.OperatingPlayer != playerIndex)
                return -3;

            var player = info.Players[playerIndex];
            if (!CanAct(player) || player.IsDisconnected)
                return -4;
            if (num <= info.CurrentHighestPlayerBet || num < player.StageBet)
                return -5;

            var availableTotal = (ulong)player.StageBet + player.Chips;
            if (num > availableTotal)
                return -4;
            if (!info.CanPlayerRaise(playerIndex))
                return -6;

            var isAllIn = num == availableTotal;
            var minimumRaiseTotal = (ulong)info.CurrentHighestPlayerBet + info.CurrentHighestRaise;
            if (!isAllIn && num < minimumRaiseTotal)
                return -5;

            var previousBet = info.CurrentHighestPlayerBet;
            var raiseIncrement = num - previousBet;
            if (!CommitChips(info, player, num - player.StageBet))
                return -7;
            info.CurrentHighestPlayerBet = num;
            if (raiseIncrement >= info.CurrentHighestRaise)
                info.CurrentHighestRaise = raiseIncrement;

            player.LastOperation = new PlayerOperation()
            {
                Type = previousBet == 0 ? PlayerOperationType.Bet : PlayerOperationType.Raise,
                Number = num,
            };
            player.HasActedThisStage = true;
            player.BetFacedAtLastAction = info.CurrentHighestPlayerBet;
            AdvanceAfterAction(info);
            return 0;
        }

        public int Call(int roomId, int playerIndex)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return -1;
            if (playerIndex < 0 || playerIndex >= info.PlayersCount)
                return -2;
            if (info.OperatingPlayer != playerIndex)
                return -3;

            var player = info.Players[playerIndex];
            if (!CanAct(player) || player.StageBet >= info.CurrentHighestPlayerBet)
                return -4;

            var required = info.CurrentHighestPlayerBet - player.StageBet;
            var committed = Math.Min(required, player.Chips);
            if (!CommitChips(info, player, committed))
                return -5;
            player.LastOperation = new PlayerOperation()
            {
                Type = committed < required ? PlayerOperationType.AllIn : PlayerOperationType.Call,
                Number = player.StageBet,
            };
            player.HasActedThisStage = true;
            player.BetFacedAtLastAction = info.CurrentHighestPlayerBet;
            AdvanceAfterAction(info);
            return 0;
        }

        public int AllIn(int roomId, int playerIndex)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return -1;
            if (playerIndex < 0 || playerIndex >= info.PlayersCount)
                return -2;
            if (info.OperatingPlayer != playerIndex)
                return -3;

            var player = info.Players[playerIndex];
            if (!CanAct(player) || player.Chips == 0 || player.IsDisconnected)
                return -4;

            var allInTotal = checked(player.StageBet + player.Chips);
            if (allInTotal > info.CurrentHighestPlayerBet && !info.CanPlayerRaise(playerIndex))
                return -5;

            var previousBet = info.CurrentHighestPlayerBet;
            if (!CommitChips(info, player, player.Chips))
                return -6;
            if (allInTotal > previousBet)
            {
                var raiseIncrement = allInTotal - previousBet;
                info.CurrentHighestPlayerBet = allInTotal;
                if (raiseIncrement >= info.CurrentHighestRaise)
                    info.CurrentHighestRaise = raiseIncrement;
            }

            player.LastOperation = new PlayerOperation()
            {
                Type = PlayerOperationType.AllIn,
                Number = player.StageBet,
            };
            player.HasActedThisStage = true;
            player.BetFacedAtLastAction = info.CurrentHighestPlayerBet;
            AdvanceAfterAction(info);
            return 0;
        }

        public int Fold(int roomId, int playerIndex)
        {
            if (!_games.TryGetValue(roomId, out var info))
                return -1;
            if (playerIndex < 0 || playerIndex >= info.PlayersCount)
                return -2;
            if (info.OperatingPlayer != playerIndex)
                return -3;

            var player = info.Players[playerIndex];
            if (!CanAct(player))
                return -4;

            player.IsFold = true;
            player.LastOperation = new PlayerOperation() { Type = PlayerOperationType.Fold };
            player.HasActedThisStage = true;
            player.BetFacedAtLastAction = info.CurrentHighestPlayerBet;
            AdvanceAfterAction(info);
            return 0;
        }

        public void GameOver(int roomId)
        {
            OnGameInfoChanged?.Invoke(roomId);
            CancelGame(roomId, "GameClosed");
            GC.Collect();
        }

        public void CancelGame(int roomId, string reason)
        {
            var gameInfo = GetGameInfo(roomId);
            if (gameInfo == null)
                return;

            gameInfo.Timer.OnTimeUp = null;
            gameInfo.Timer.Stop();
            if (gameInfo.Stage != GameStage.RoundOver && gameInfo.Stage != GameStage.GameOver)
                SafeAbortHand(gameInfo.HandId, reason);
            _autoRunningRooms.Remove(roomId);
            _games.Remove(roomId);
        }

        private void SafeAbortHand(string handId, string reason)
        {
            try
            {
                _accountRepository?.AbortHand(handId, reason);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Unable to abort hand {handId}: {exception}");
            }
        }
    }
}
