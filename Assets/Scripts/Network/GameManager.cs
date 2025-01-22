using Cysharp.Threading.Tasks;
using Framework.Log;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Network
{
    /// <summary>
    /// 与<see cref="GameController"/>不同，此类管理的是服务器上开始的游戏的信息
    /// </summary>
    public class GameManager
    {
        /// <summary>
        /// key 为 roomId
        /// </summary>
        private Dictionary<int, ServerGameInfo> _games = new Dictionary<int, ServerGameInfo>();

        public event System.Action<int> OnGameInfoChanged;
        public event System.Action<int> OnGameStageEnded;
        public event System.Action<int, RoundResult> OnRoomRoundEnded;
        public event System.Action<int, GameResult> OnRoomGameOver;


        /// <summary>
        /// 根据该房间信息，创建一场游戏
        /// </summary>
        /// <param name="room"></param>
        public void CreateGame(RoomInfo room)
        {
            var gameInfo = ServerGameInfo.Create(room);
            _games.Add(room.Id, gameInfo);
            gameInfo.Timer.OnTimeUp += () =>
            {
                AutoOperation(gameInfo);
            };
        }

        public ServerGameInfo GetGameInfo(int roomId)
        {
            if (_games.ContainsKey(roomId))
                return _games[roomId];
            else
                return null;
        }

        private void AllInPotDivision(ServerGameInfo info)
        {
            // 先把原来的奖池拿出
            var oldPot = info.Pots.Pop();

            // 根据下注大小排序
            // 其余人既然通过不是Allin而留了下来，那么他们下的注一定比下注最多的AllIn玩家下的注还要多
            info.NewAllInPlayers.Sort((a, b) =>
            {
                if (info.Players[a].RoundBet < info.Players[b].RoundBet)
                    return -1;
                else
                    return 1;
            });
            info.NewFoldPlayers.Sort((a, b) =>
            {
                if (info.Players[a].RoundBet < info.Players[b].RoundBet)
                    return -1;
                else
                    return 1;
            });

            // 加注留下来的玩家
            var calledPlayers = oldPot.JoinedPlayers
                .Except(info.NewAllInPlayers)
                .Except(info.NewFoldPlayers)
                .ToList();

            Log.WriteInfo($"Detected new All-In player in room {info.RoomInfo.Id}. Starting to divide the pot...");

            // 这里使用AllInQueue的原因，是因为每次分割完奖池后，一个AllIn玩家就不会参与接下来的奖池分配了
            // allIn的玩家
            var allinQueue = new Queue<int>(info.NewAllInPlayers);
            var roundBets = info.Players.Select(player => player.RoundBet).ToArray();
            while (allinQueue.Count > 0)
            {
                // 这里进入的玩家，一定是下注比AllIn的玩家还要多的玩家
                var newPotJoinedPlayers = calledPlayers
                    .Concat(allinQueue)
                    .ToList();

                var newPot = new ServerGameInfo.Pot()
                {
                    JoinedPlayers = newPotJoinedPlayers.OrderBy(player => player).ToList(),
                };

                var allInPlayerIndex = allinQueue.Dequeue();
                var allInBet = roundBets[allInPlayerIndex];
                if (allInBet == 0)
                    continue;

                // 此allin可以收到其他参与玩家的筹码，因为参与玩家的下注的筹码一定比allin玩家多
                newPot.ChipCount += Convert.ToUInt32(newPot.JoinedPlayers.Count * allInBet);
                newPotJoinedPlayers.ForEach(index => roundBets[index] -= allInBet);

                for (int j = 0; j < info.NewFoldPlayers.Count; j++)
                {
                    var foldPlayerIndex = info.NewFoldPlayers[j];
                    // 一个AllIn的玩家总能从Fold的玩家处收到筹码
                    // 如果Fold的玩家筹码很多，那么AllIn玩家只能收到一部分
                    if (roundBets[foldPlayerIndex] >= allInBet)
                    {
                        newPot.ChipCount += allInBet;
                        roundBets[foldPlayerIndex] -= allInBet;

                    }
                    else
                    {
                        // 如果Fold的玩家筹码很少，那么AllIn玩家收到全部
                        // 如果已经被其他AllIn玩家分完，那么就没法再收了
                        newPot.ChipCount += roundBets[foldPlayerIndex];
                        roundBets[foldPlayerIndex] = 0;
                    }
                }

                Log.WriteInfo($"Room {info.RoomInfo}: Pot bet = {allInBet}; Total Chips = {newPot.ChipCount}.");

                // 将新的奖池放入
                info.Pots.Push(newPot);

                // 老奖池少掉这么一部分钱
                oldPot.ChipCount -= newPot.ChipCount;
            }

            //奖池分割完后, 剩余的钱再给所有Call的人分
            if(oldPot.ChipCount > 0)
            {
                var finalPot = new ServerGameInfo.Pot()
                {
                    ChipCount = oldPot.ChipCount,
                    JoinedPlayers = calledPlayers,
                };
                Log.WriteInfo($"Room {info.RoomInfo}: Last pot bet: Total Chips = {finalPot.ChipCount}.");
                info.Pots.Push(finalPot);
            }
        }

        private void PreflopExiting(ServerGameInfo info)
        {
            info.Stage = GameStage.Flop;

            // 开始让小盲下注
            // 如果小盲allin或者fold了甚至是掉线了，那就找下一个人
            info.FirstBet = info.SmallBlind;
            while (info.Players[info.FirstBet].IsFold || info.Players[info.FirstBet].IsAllIn || info.Players[info.FirstBet].IsDisconnected)
            {
                info.FirstBet = (info.FirstBet + 1) % info.PlayersCount;
            }
            info.OperatingPlayer = info.FirstBet;

            // 翻出三张牌
            info.Community.Add(info.Deck.Dequeue());
            info.Community.Add(info.Deck.Dequeue());
            info.Community.Add(info.Deck.Dequeue());

            // 调整加注信息
            info.LastHighestRaise = Math.Max(info.LastHighestRaise, info.CurrentHighestPlayerBet);
            info.CurrentHighestRaise = 0;
            info.CurrentHighestPlayerBet = 0;


            foreach (var playerInfo in info.Players)
            {
                // 刷新此轮下注信息
                playerInfo.StageBet = 0;
                // 清除上次操作
                playerInfo.LastOperation = null;
            }

            OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
        }

        private void FlopExiting(ServerGameInfo info)
        {
            info.Stage = GameStage.Turn;

            // 开始让小盲下注
            // 如果小盲allin或者fold了，那就找下一个人
            info.FirstBet = info.SmallBlind;
            while (info.Players[info.FirstBet].IsFold || info.Players[info.FirstBet].IsAllIn || info.Players[info.FirstBet].IsDisconnected)
            {
                info.FirstBet = (info.FirstBet + 1) % info.PlayersCount;
            }
            info.OperatingPlayer = info.FirstBet;

            // 翻出一张牌
            info.Community.Add(info.Deck.Dequeue());

            // 调整加注信息
            info.LastHighestRaise = Math.Max(info.LastHighestRaise, info.CurrentHighestPlayerBet);
            info.CurrentHighestRaise = 0;
            info.CurrentHighestPlayerBet = 0;

            foreach (var playerInfo in info.Players)
            {
                // 刷新此轮下注信息
                playerInfo.StageBet = 0;
                // 清除上次操作
                playerInfo.LastOperation = null;
            }

            OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
        }

        private void TurnExiting(ServerGameInfo info)
        {
            info.Stage = GameStage.River;

            // 开始让小盲下注
            // 如果小盲allin或者fold了，那就找下一个人
            info.FirstBet = info.SmallBlind;
            while (info.Players[info.FirstBet].IsFold || info.Players[info.FirstBet].IsAllIn || info.Players[info.FirstBet].IsDisconnected)
            {
                info.FirstBet = (info.FirstBet + 1) % info.PlayersCount;
            }
            info.OperatingPlayer = info.FirstBet;

            // 翻出一张牌
            info.Community.Add(info.Deck.Dequeue());

            // 调整加注信息
            info.LastHighestRaise = Math.Max(info.LastHighestRaise, info.CurrentHighestPlayerBet);
            info.CurrentHighestRaise = 0;
            info.CurrentHighestPlayerBet = 0;

            foreach (var playerInfo in info.Players)
            {
                // 刷新此轮下注信息
                playerInfo.StageBet = 0;
                // 清除上次操作
                playerInfo.LastOperation = null;
            }
            OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
        }

        private void RiverExiting(ServerGameInfo info)
        {
            info.Stage = GameStage.RoundOver;
            info.OperatingPlayer = -1;
            // 判断胜负，并分配奖池
            // 每个玩家最终最大的牌型
            List<(int Player, PokerPattern Pattern)> patterns = new List<(int, PokerPattern)>();
            for (int i = 0; i < info.PlayersCount; i++)
            {
                // 盖牌了就没权利比牌
                // 一个玩家如果只是掉线了，还是有权利比牌的，如果AllIn之后掉线，那么肯定有权利继续比牌
                // 但轮到掉线的玩家时，总会自动采取Check和Fold，大概率掉线的玩家是不能参与比牌的
                if (info.Players[i].IsFold)
                    continue;
                var pokers = info.Community.Concat(info.Players[i].Hole);
                var pattern = PokerUtility.GetBiggestPattern(pokers.ToList(), out _);
                patterns.Add((i, pattern));
            }

            // 排序，按牌型从大到小，牌型一样则按Index从小到大排列
            patterns.Sort((a, b) =>
            {
                if (a.Pattern.BiggerThan(b.Pattern))
                    return -1;
                else if (a.Pattern.SmallerThan(b.Pattern))
                    return 1;
                else
                    return a.Player - b.Player;
            });

            uint[] earned = new uint[info.PlayersCount];
            System.Array.Fill<uint>(earned, 0);
            // 分奖池
            foreach (var pot in info.Pots)
            {
                if (pot.ChipCount == 0)
                    continue;
                List<int> winPlayers = new List<int>();
                for (int i = 0; i < patterns.Count; i++)
                {
                    (int player, PokerPattern pattern) = patterns[i];
                    // 这个奖池如果该玩家分不了，那就直接下一位
                    if (!pot.JoinedPlayers.Contains(player))
                        continue;
                    // 如果能分，那去找所有牌力一样大，也能分的玩家，平分奖池
                    // patterns是按从大到小排序的，先寻找一样大小的Pattern，这些人会平分奖池
                    winPlayers.Add(player);
                    for (int offset = 1; i + offset < patterns.Count; i++)
                    {
                        (int nextPlayer, PokerPattern nextPattern) = patterns[i + offset];
                        if (!nextPattern.SameTo(pattern))
                            break;

                        if (pot.JoinedPlayers.Contains(nextPlayer))
                            winPlayers.Add(nextPlayer);
                    }
                    break;
                }

                var count = System.Convert.ToUInt32(winPlayers.Count);

                // 分配除尽的部分 
                var part = pot.ChipCount / count;
                foreach (var p in winPlayers)
                {
                    info.Players[p].Chips += part;
                    earned[p] += part;
                }

                // 余数部分按照庄家的位置依次分配
                // players[j]代表着dealer的左手边第一个能分到奖池的人的位置
                var left = pot.ChipCount % count;
                int j = 0;
                while (j < winPlayers.Count && winPlayers[j] < info.Dealer)
                    j++;
                j = j % winPlayers.Count;
                while (left > 0)
                {
                    info.Players[winPlayers[j]].Chips++;
                    earned[winPlayers[j]]++;
                    left--;
                    j = (j + 1) % winPlayers.Count;
                }
            }

            info.Pots.Clear();

            // 此次游戏结束，观察是否有人筹码为0，为0则结束游戏
            foreach (var playerInfo in info.Players)
            {
                if (playerInfo.Chips == 0)
                {
                    info.Stage = GameStage.GameOver;
                    break;
                }
            }

            // 通知此轮游戏结束
            var roundResult = new RoundResult() { Community = info.Community };
            for (int i = 0; i < earned.Length; i++)
            {
                roundResult.PlayerResults.Add(new RoundResult.Player()
                {
                    PlayerName = info.RoomInfo.Players[i],
                    Hole = info.Players[i].Hole,
                    IsFold = info.Players[i].IsFold,
                    IsDisconnected = info.Players[i].IsDisconnected,
                    ChipEarned = earned[i],
                });
            }
            OnRoomRoundEnded?.Invoke(info.RoomInfo.Id, roundResult);

            if (info.Stage == GameStage.GameOver)
            {
                var gameResult = new GameResult();
                foreach (var player in info.Players)
                {
                    gameResult.PlayerResults.Add(
                        new GameResult.Player()
                        {
                            Name = player.PlayerName,
                            Chips = player.Chips,
                            IsDisconnected = player.IsDisconnected
                        }
                    );
                }

                // 这里不使游戏结束，而使外界控制
                OnRoomGameOver?.Invoke(info.RoomInfo.Id, gameResult);
            }

        }

        private void EarlyTermination(ServerGameInfo info)
        {
            Log.WriteInfo("Detected that there's only one player who is not fold and not disconnected, so the game begin to terminate early.");
            // 提早结束
            // 提早结束只会有一个玩家没有fold
            // fold了的玩家，钱不可能为0，因此提早结束不可能会导致整句游戏结束
            info.Stage = GameStage.RoundOver;
            var roundResult = new RoundResult() { Community = info.Community };
            for (int i = 0; i < info.PlayersCount; i++)
            {
                roundResult.PlayerResults.Add(new RoundResult.Player()
                {
                    PlayerName = info.RoomInfo.Players[i],
                    Hole = info.Players[i].Hole,
                    IsFold = info.Players[i].IsFold,
                    ChipEarned = info.Players[i].IsFold ? 0 : info.TotalPot,
                });
                if (!info.Players[i].IsFold)
                    info.Players[i].Chips += info.TotalPot;
            }

            OnRoomRoundEnded?.Invoke(info.RoomInfo.Id, roundResult);
        }

        private async void Auto(ServerGameInfo info)
        {
            info.OperatingPlayer = -1;
            Log.WriteInfo($"Game in room {info.RoomInfo.Id} is in auto play mode.");
            while (info.Stage != GameStage.RoundOver && info.Stage != GameStage.GameOver)
            {
                switch (info.Stage)
                {
                    case GameStage.Preflop:
                        info.Stage = GameStage.Flop;
                        info.Community.Add(info.Deck.Dequeue());
                        info.Community.Add(info.Deck.Dequeue());
                        info.Community.Add(info.Deck.Dequeue());
                        OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
                        break;
                    case GameStage.Flop:
                        info.Stage = GameStage.Turn;
                        info.Community.Add(info.Deck.Dequeue());
                        OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
                        break;
                    case GameStage.Turn:
                        info.Stage = GameStage.River;
                        info.Community.Add(info.Deck.Dequeue());
                        OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
                        break;
                    case GameStage.River:
                        RiverExiting(info);
                        break;
                }
                await UniTask.WaitForSeconds(3);
            }

        }

        /// <summary>
        /// 让此房间中进入下一次游戏（翻牌、下注、开牌）
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns>0 = success, -1 = room doesn't exist, -2 = game already over</returns>
        public int StartNew(int roomId)
        {
            if (!_games.ContainsKey(roomId))
                return -1;

            ServerGameInfo info = _games[roomId];

            Log.WriteInfo($"A new round of game in room {info.RoomInfo.Id} is started.");

            // 生成一幅全新的牌
            info.Deck = Poker.RandomDeck();

            // 刷新玩家个人信息，并为每位玩家发牌
            foreach (var playerInfo in info.Players)
            {
                playerInfo.IsAllIn = false;
                playerInfo.IsFold = false;
                playerInfo.StageBet = 0;
                playerInfo.RoundBet = 0;
                playerInfo.LastOperation = null;
                playerInfo.Hole.Clear();

                playerInfo.Hole.Add(info.Deck.Dequeue());
                playerInfo.Hole.Add(info.Deck.Dequeue());
            }

            // 更改轮次信息
            info.Stage = GameStage.Preflop;

            // 刷新奖池
            info.TotalPot = 0;
            info.Pots.Clear();
            info.Pots.Push(new ServerGameInfo.Pot()
            {
                JoinedPlayers = new List<int>(),
                ChipCount = 0,
            });
            for (int i = 0; i < info.PlayersCount; i++)
                if (!info.Players[i].IsDisconnected)
                    info.Pots.Peek().JoinedPlayers.Add(i);

            // 清除公共牌
            info.Community.Clear();

            // 清除历史下注信息
            info.LastHighestRaise = 0;
            info.CurrentHighestRaise = 0;
            info.CurrentHighestPlayerBet = 0;
            // 重置列表
            info.NewAllInPlayers.Clear();
            info.NewFoldPlayers.Clear();

            // 更换大小盲
            info.Dealer = (info.Dealer + 1) % info.RoomInfo.PlayerCount;
            info.SmallBlind = (info.Dealer + 1) % info.RoomInfo.PlayerCount;
            while (info.Players[info.SmallBlind].IsDisconnected)
                info.SmallBlind = (info.SmallBlind + 1) % info.RoomInfo.PlayerCount;
            info.BigBlind = (info.SmallBlind + 1) % info.RoomInfo.PlayerCount;
            while (info.Players[info.BigBlind].IsDisconnected)
                info.BigBlind = (info.BigBlind + 1) % info.RoomInfo.PlayerCount;

            info.OperatingPlayer = info.SmallBlind;
            info.FirstBet = info.SmallBlind;

            // 大小盲下注
            ServerGameInfo.PlayerInfo smallBlind = info.Players[info.SmallBlind];
            ServerGameInfo.PlayerInfo bigBlind = info.Players[info.BigBlind];
            var leastChip = System.Convert.ToUInt32(info.RoomInfo.SmallBlindChips);
            if (smallBlind.Chips <= leastChip)
            {
                AllIn(roomId, info.SmallBlind);
            }
            else
            {
                Bet(roomId, info.SmallBlind, leastChip);
            }
            if (bigBlind.Chips <= 2 * leastChip)
                AllIn(roomId, info.BigBlind);
            else
            {
                Bet(roomId, info.BigBlind, 2 * leastChip);
            }


            info.BigBlindRaise = true;

            info.Timer.Start();
            return 0;
        }

        /// <summary>
        /// 让此房间中进入下一个轮次
        /// </summary>
        /// <param name="roomId">0 = success, -1 = room doesn't exist</param>
        public int NextStage(int roomId)
        {
            if (!_games.ContainsKey(roomId))
                return -1;

            ServerGameInfo info = _games[roomId];

            // 如果其他人全部掉线，那么就直接游戏结束
            int totalDisconnectedPlayer = info.Players.Where(player => !player.IsDisconnected).Count();
            if (totalDisconnectedPlayer <= 1)
            {
                var gameResult = new GameResult();
                foreach (var player in info.Players)
                {
                    gameResult.PlayerResults.Add(
                        new GameResult.Player()
                        {
                            Name = player.PlayerName,
                            Chips = player.Chips,
                            IsDisconnected = player.IsDisconnected
                        }
                    );
                }

                // 这里不使游戏结束，而使外界控制
                OnRoomGameOver?.Invoke(info.RoomInfo.Id, gameResult);
                return 0;
            }

            // 在新的一轮中，如果只有一个人没有Fold，那么直接结算
            int totalNotFoldPlayer = info.Players.Where(player => !player.IsFold).Count();
            if (totalNotFoldPlayer <= 1)
            {
                EarlyTermination(info);
                return 0;
            }

            // 根据刚才这轮是否有人AllIn,来划分奖池
            if (info.NewAllInPlayers.Count > 0)
                AllInPotDivision(info);


            // 在新的一轮中，少于等于一个人没有AllIn和盖牌或掉线，转为自动模式
            var nonFoldAndAllInPlayerCount = info.Players.Where(player => !player.IsAllIn && !player.IsFold && !player.IsDisconnected).Count();
            if (nonFoldAndAllInPlayerCount <= 1)
            {
                Auto(info);
                return 0;
            }

            // 重置列表
            info.NewAllInPlayers.Clear();
            info.NewFoldPlayers.Clear();

            Log.WriteInfo($"Game in room {info.RoomInfo.Id} goes to next stage from stage {info.Stage}.");

            switch (info.Stage)
            {
                case GameStage.Preflop:
                    PreflopExiting(info);
                    break;
                case GameStage.Flop:
                    FlopExiting(info);
                    break;
                case GameStage.Turn:
                    TurnExiting(info);
                    break;
                case GameStage.River:
                    RiverExiting(info);
                    break;
            }

            return 0;
        }


        /// <summary>
        /// 将一个玩家的状态设置为掉线
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="playerName"></param>
        public void SetDisconnected(int roomId, string playerName)
        {
            if (!_games.ContainsKey(roomId))
                return;

            ServerGameInfo info = _games[roomId];

            var playerIndex = info.Players.FindIndex(player => player.PlayerName == playerName);
            if (playerIndex < 0)
                return;
            info.Players[playerIndex].IsDisconnected = true;

            if (playerIndex != info.OperatingPlayer)
                return;

            // 如果当前是该玩家操作，自动帮其选择Check或者fold
            int result = Fold(roomId, playerIndex);
            if (result != 0)
            {
                Check(roomId, playerIndex);
            }
        }

        /// <summary>
        /// 使当前玩家做出自动操作
        /// </summary>
        /// <param name="info"></param>
        private void AutoOperation(ServerGameInfo info)
        {
            // 首先尝试fold，fold失败后再check
            if (info.OperatingPlayer == -1)
                return;
            int result = Fold(info.RoomInfo.Id, info.OperatingPlayer);
            if (result != 0)
            {
                Check(info.RoomInfo.Id, info.OperatingPlayer);
                Log.WriteInfo($"Time up! The player {info.Players[info.OperatingPlayer].PlayerName} in room {info.RoomInfo.Id} is forced by system to execute operation \"Fold\".");
            }
            else
                Log.WriteInfo($"Time up! The player {info.Players[info.OperatingPlayer].PlayerName} in room {info.RoomInfo.Id} is forced by system to execute operation \"Check\".");
        }

        /// <summary>
        /// 将控制权转交给下一个玩家
        /// </summary>
        /// <param name="info"></param>
        private void NextPlayer(ServerGameInfo info, PlayerOperationType type)
        {
            // 如果当前是Preflop阶段，此玩家是大盲，且此玩家使用Check，那么就直接开启下一轮
            if (info.Stage == GameStage.Preflop &&
                info.OperatingPlayer == info.BigBlind && type == PlayerOperationType.Check)
            {
                OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
                OnGameStageEnded?.Invoke(info.RoomInfo.Id);
                return;
            }

            // 否则，寻找下一位可以操作的玩家
            while (true)
            {

                info.OperatingPlayer = (info.OperatingPlayer + 1) % info.PlayersCount;

                // 如果转完了一圈
                if (info.OperatingPlayer == info.FirstBet)
                {
                    // 但有例外：Preflop阶段如果大盲还没有加过注，那么还可以选择一次是否加注
                    bool exception = info.BigBlindRaise && info.Stage == GameStage.Preflop &&
                        info.OperatingPlayer == info.BigBlind;
                    if (!exception)
                    {
                        OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
                        OnGameStageEnded?.Invoke(info.RoomInfo.Id);
                        break;
                    }
                    else
                    {
                        info.BigBlindRaise = false;
                        // 这种例外情况下，如果大盲没掉线，那么就给大盲操作，如果掉线了，大盲就自动Check
                        if (info.Players[info.OperatingPlayer].IsDisconnected)
                        {
                            Check(info.RoomInfo.Id, info.OperatingPlayer);
                            break;
                        }
                        else
                        {
                            // 将新的信息玩家信息告诉其他人
                            OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
                            break;
                        }
                    }
                }

                // 如果找到了一个不是Fold也不是AllIn的人，就把控制权给他
                if (!info.Players[info.OperatingPlayer].IsFold && !info.Players[info.OperatingPlayer].IsAllIn)
                {
                    // 如果该玩家掉线，那么让该玩家执行自动操作
                    if (info.Players[info.OperatingPlayer].IsDisconnected)
                    {
                        AutoOperation(info); // 会包含Check或Fold，在那里会调用OnGameInfoChanged
                    }
                    else
                        // 将新的信息玩家信息告诉其他人
                        OnGameInfoChanged?.Invoke(info.RoomInfo.Id);
                    break;
                }
            }
        }

        /// <summary>
        /// 此房间中的一位玩家进行了Check
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="playerIndex"></param>
        /// <returns>0 = success, -1 = room doesn't exist, -2 = cannot find player, -3 = not operating, -4 = cannot check</returns>
        public int Check(int roomId, int playerIndex)
        {
            if (!_games.ContainsKey(roomId))
                return -1;

            ServerGameInfo info = _games[roomId];

            if (playerIndex < 0 || playerIndex >= info.PlayersCount)
                return -2;

            if (info.OperatingPlayer != playerIndex)
                return -3;

            ServerGameInfo.PlayerInfo playerInfo = info.Players[playerIndex];

            // 如果有人下注比你高，就不能Check
            if (info.CurrentHighestPlayerBet > playerInfo.StageBet)
                return -4;
            // All in了也没法Check
            if (playerInfo.IsAllIn)
                return -4;

            // 以上情况排除后，到该玩家选择Check时，之前进行的所有玩家都一定是Check

            // 设置上次操作
            playerInfo.LastOperation = new PlayerOperation() { Type = PlayerOperationType.Check };


            if (info.Timer.IsCounting)
                info.Timer.Clear();
            else
                info.Timer.Start();

            // 将控制权交给下一位没有盖牌也没有allin的玩家
            // 如果是Preflop阶段的大盲的话，选择Check就直接进下一个Stage了
            if (info.BigBlind == playerIndex && info.Stage == GameStage.Preflop)
            {
                OnGameInfoChanged?.Invoke(roomId);
                OnGameStageEnded?.Invoke(roomId);
            }
            else
            {
                NextPlayer(info, PlayerOperationType.Check);
            }

            Log.WriteInfo($"The player {playerInfo.PlayerName} in room {roomId} executed operation \"Check\".");
            return 0;
        }


        /// <summary>
        /// 此房间中的一位玩家进行了Bet或Raise（加注）
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="playerIndex"></param>
        /// <param name="num"></param>
        /// <returns>0 = success, -1 = room doesn't exist, -2 = cannot find player, -3 = not operating, -4 = too few chips, -5 = too few bet, </returns>
        public int Bet(int roomId, int playerIndex, uint num)
        {
            if (!_games.ContainsKey(roomId))
                return -1;

            ServerGameInfo info = _games[roomId];

            if (playerIndex < 0 || playerIndex >= info.PlayersCount)
                return -2;

            if (info.OperatingPlayer != playerIndex)
                return -3;

            ServerGameInfo.PlayerInfo playerInfo = info.Players[playerIndex];

            // 加注首先需要手头上有足够的筹码
            // 如果筹码刚好够，也不视为加注，而是AllIn，不能使用该函数
            if (playerInfo.Chips <= num)
                return -4;
            // All in了也没法Bet
            if (playerInfo.IsAllIn)
                return -4;

            // 加注一定要比之前下注的最高的玩家还要高，而且要高于一个最小加注量
            var leastRaise = info.LeastRaise;
            if (num < info.CurrentHighestPlayerBet + leastRaise)
                return -5;

            // 扣除筹码
            var added = num - playerInfo.StageBet;
            playerInfo.Chips -= added;
            playerInfo.StageBet = num;
            playerInfo.RoundBet += added;

            // 奖池增加
            info.TotalPot += added;
            info.Pots.Peek().ChipCount += added;

            // 加注后，你将成为起始操作玩家的位置
            info.FirstBet = playerIndex;

            // 设置上次操作
            playerInfo.LastOperation = new PlayerOperation()
            {
                Type = info.CurrentHighestPlayerBet == 0 ? PlayerOperationType.Bet : PlayerOperationType.Raise,
                Number = num
            };

            // 加注后，你将成为此轮下注最高的玩家，同时此轮最高加注的筹码量也会增加
            info.CurrentHighestRaise = num - info.CurrentHighestPlayerBet;
            info.CurrentHighestPlayerBet = playerInfo.StageBet;

            // 如果是大盲，任意一次raise都会使得你失去机会
            info.BigBlindRaise = false;

            if (info.Timer.IsCounting)
                info.Timer.Clear();
            else
                info.Timer.Start();

            // 将控制权交给下一位没有盖牌也没有allin的玩家
            NextPlayer(info, PlayerOperationType.Bet);

            Log.WriteInfo($"The player {playerInfo.PlayerName} in room {roomId} executed operation \"Bet\" or \"Raise\".");
            return 0;
        }

        /// <summary>
        /// 此房间的一位玩家进行了跟注
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="playerIndex"></param>
        /// <returns>0 = success, -1 = room doesn't exist, -2 = cannot find player, -3 = not operating, -4 = too few chips,</returns>
        public int Call(int roomId, int playerIndex)
        {
            if (!_games.ContainsKey(roomId))
                return -1;

            ServerGameInfo info = _games[roomId];

            if (playerIndex < 0 || playerIndex >= info.PlayersCount)
                return -2;

            if (info.OperatingPlayer != playerIndex)
                return -3;

            ServerGameInfo.PlayerInfo playerInfo = info.Players[playerIndex];

            // 如果筹码不够，是无法跟注的
            // 如果筹码刚好够，也不视为跟注，而是AllIn，不能使用该函数
            var need = info.CurrentHighestPlayerBet - playerInfo.StageBet;
            if (playerInfo.Chips <= need)
                return -4;
            // All in了也没法Call
            if (playerInfo.IsAllIn)
                return -4;

            // 跟注就直接加到和最高下注玩家相同即可

            // 设置上次操作
            playerInfo.LastOperation = new PlayerOperation() { Type = PlayerOperationType.Call, Number = info.CurrentHighestPlayerBet };

            // 扣除筹码
            playerInfo.Chips -= need;
            playerInfo.StageBet += need;
            playerInfo.RoundBet += need;

            // 奖池增加
            info.TotalPot += need;
            info.Pots.Peek().ChipCount += need;

            // 将控制权交给下一位没有盖牌也没有allin的玩家
            NextPlayer(info, PlayerOperationType.Call);

            if (info.Timer.IsCounting)
                info.Timer.Clear();
            else
                info.Timer.Start();

            OnGameInfoChanged?.Invoke(roomId);

            Log.WriteInfo($"The player {playerInfo.PlayerName} in room {roomId} executed operation \"Call\".");
            return 0;
        }

        /// <summary>
        /// 此房间中的一位玩家进行了All In
        /// All in 不受筹码量的限制
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="playerIndex"></param>
        /// <returns>0 = success, -1 = room doesn't exist, -2 = cannot find player, -3 = not operating</returns>
        public int AllIn(int roomId, int playerIndex)
        {
            if (!_games.ContainsKey(roomId))
                return -1;

            ServerGameInfo info = _games[roomId];

            if (playerIndex < 0 || playerIndex >= info.PlayersCount)
                return -2;

            if (info.OperatingPlayer != playerIndex)
                return -3;

            ServerGameInfo.PlayerInfo playerInfo = info.Players[playerIndex];

            var allinChips = playerInfo.Chips;

            // 扣除筹码
            playerInfo.Chips = 0;
            playerInfo.StageBet += allinChips;
            playerInfo.RoundBet += allinChips;

            // 设置上次操作
            playerInfo.LastOperation = new PlayerOperation() { Type = PlayerOperationType.AllIn, Number = playerInfo.RoundBet };

            // 奖池增加
            info.TotalPot += allinChips;
            info.Pots.Peek().ChipCount += allinChips;

            // 如果AllIn时不仅变成了最高的下注玩家，而且加注的数量还超过了最高加注量，则修改最高下注数量和最高加注量
            if (playerInfo.StageBet > info.CurrentHighestPlayerBet + info.CurrentHighestRaise)
            {
                info.CurrentHighestRaise = playerInfo.StageBet - info.CurrentHighestPlayerBet;
                info.CurrentHighestPlayerBet = playerInfo.StageBet;
                // 作为全场最高，你是第一个开始的
                info.FirstBet = playerIndex;
            }
            // 如果AllIn后，只变成了最高的下注玩家，则只修改最高下注量
            else if (playerInfo.StageBet > info.CurrentHighestPlayerBet)
            {
                info.CurrentHighestPlayerBet = playerInfo.StageBet;
                // 作为全场最高，你是第一个开始的
                info.FirstBet = playerIndex;
            }

            // 设置为AllIn
            playerInfo.IsAllIn = true;
            info.NewAllInPlayers.Add(playerIndex);

            // 将控制权交给下一位没有盖牌也没有allin的玩家
            NextPlayer(info, PlayerOperationType.AllIn);

            if (info.Timer.IsCounting)
                info.Timer.Clear();
            else
                info.Timer.Start();

            OnGameInfoChanged?.Invoke(roomId);

            Log.WriteInfo($"The player {playerInfo.PlayerName} in room {roomId} executed operation \"All In\".");
            return 0;
        }

        /// <summary>
        /// 此房间中的一位玩家进行了Fold
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="playerIndex"></param>
        /// <returns>0 = success, -1 = room doesn't exist, -2 = cannot find player, -3 = not operating, -4 = can't fold</returns>
        public int Fold(int roomId, int playerIndex)
        {
            if (!_games.ContainsKey(roomId))
                return -1;

            ServerGameInfo info = _games[roomId];

            if (playerIndex < 0 || playerIndex >= info.PlayersCount)
                return -2;

            if (info.OperatingPlayer != playerIndex)
                return -3;

            // 例外：Preflop阶段，其他人都check的话，大盲没有fold的权利
            bool exception =
                info.Stage == GameStage.Preflop &&
                playerIndex == info.BigBlind &&
                info.CurrentHighestPlayerBet == info.Players[playerIndex].StageBet;
            if (exception)
                return -4;

            ServerGameInfo.PlayerInfo playerInfo = info.Players[playerIndex];
            playerInfo.IsFold = true;
            info.NewFoldPlayers.Add(playerIndex);

            // 设置上次操作
            playerInfo.LastOperation = new PlayerOperation() { Type = PlayerOperationType.Fold };


            // 将控制权交给下一位没有盖牌也没有allin的玩家
            NextPlayer(info, PlayerOperationType.Fold);

            if (info.Timer.IsCounting)
                info.Timer.Clear();
            else
                info.Timer.Start();

            OnGameInfoChanged?.Invoke(roomId);


            Log.WriteInfo($"The player {playerInfo.PlayerName} in room {roomId} executed operation \"Fold\".");

            return 0;
        }

        /// <summary>
        /// 结束一个房间的游戏
        /// </summary>
        /// <param name="roomId"></param>
        public void GameOver(int roomId)
        {
            OnGameInfoChanged?.Invoke(roomId);
            var gameInfo = GetGameInfo(roomId);
            if (gameInfo == null)
                return;
            gameInfo.Timer.OnTimeUp = null;
            _games.Remove(roomId);
            GC.Collect();
        }
    }
}
