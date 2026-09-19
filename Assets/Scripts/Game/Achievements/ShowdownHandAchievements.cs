using System.Linq;
using Game.Events;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    public abstract class ShowdownHandAchievement : HandAchievement, IAchievementHandler<HandCardsRevealedEvent>
    {
        public override string Category => AchievementCategories.Hand;
        public override string IconPath => "UI/Achievements/Cards-v1";
        protected abstract PokerHandRank Rank { get; }

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || !context.Showdown) return;
            var player = context.Players.FirstOrDefault(p => p.AccountId == accountId);
            if (player != null && !player.Folded && player.BestRank == Rank)
            {
                progress.Count = Target;
            }
        }

        public void Evaluate(HandCardsRevealedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            if (playerEvent.AccountId != accountId || playerEvent.Hole.Count != 2 ||
                playerEvent.Community.Count < 3 || playerEvent.Community.Count > 5) return;
            if (AchievementPatternEvaluator.GetBest(playerEvent.Community.Concat(playerEvent.Hole).ToArray()).Rank == Rank)
            {
                progress.Count = Target;
            }
        }
    }

    public sealed class ShowdownPairAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_pair";
        public override string Name => "对对碰";
        public override string Description => "摊牌时，最佳牌型为一对。";
        public override int SortOrder => 200;
        public override Reward Reward => new Reward(500);
        protected override PokerHandRank Rank => PokerHandRank.Pair;
    }

    public sealed class ShowdownTwoPairAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_two_pair";
        public override string Name => "对对对碰";
        public override string Description => "摊牌时，最佳牌型为两对。";
        public override int SortOrder => 201;
        public override Reward Reward => new Reward(1000);
        protected override PokerHandRank Rank => PokerHandRank.Two_Pair;
    }

    public sealed class ShowdownThreeOfAKindAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_three_of_a_kind";
        public override string Name => "三连";
        public override string Description => "摊牌时，最佳牌型为三条。";
        public override int SortOrder => 202;
        public override Reward Reward => new Reward(2000);
        protected override PokerHandRank Rank => PokerHandRank.Three_of_a_Kind;
    }

    public sealed class ShowdownStraightAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_straight";
        public override string Name => "都连起来吧";
        public override string Description => "摊牌时，最佳牌型为顺子。";
        public override int SortOrder => 203;
        public override Reward Reward => new Reward(3000);
        protected override PokerHandRank Rank => PokerHandRank.Straight;
    }

    public sealed class ShowdownFlushAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_flush";
        public override string Name => "清一色";
        public override string Description => "摊牌时，最佳牌型为同花。";
        public override int SortOrder => 204;
        public override Reward Reward => new Reward(4000);
        protected override PokerHandRank Rank => PokerHandRank.Flush;
    }

    public sealed class ShowdownFullHouseAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_full_house";
        public override string Name => "爷爷！爷爷！";
        public override string Description => "摊牌时，最佳牌型为葫芦。";
        public override int SortOrder => 205;
        public override Reward Reward => new Reward(5000);
        protected override PokerHandRank Rank => PokerHandRank.Full_House;
        public override string IconPath => "UI/Achievements/FullHouse";
    }

    public sealed class ShowdownFourOfAKindAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_four_of_a_kind";
        public override string Name => "炸弹人";
        public override string Description => "摊牌时，最佳牌型为四条。";
        public override int SortOrder => 206;
        public override Reward Reward => new Reward(8000);
        protected override PokerHandRank Rank => PokerHandRank.Four_of_a_Kind;
    }

    public sealed class ShowdownStraightFlushAchievement : ShowdownHandAchievement
    {
        public override string Id => "showdown_straight_flush";
        public override string Name => "“同花打不打得过俘虏豪斯？”";
        public override string Description => "摊牌时，最佳牌型为同花顺。";
        public override int SortOrder => 207;
        public override Reward Reward => new Reward(20000, new[] { new ItemStack("namecard-straight-flush", 1) });
        protected override PokerHandRank Rank => PokerHandRank.Straight_Flush;
    }
}
