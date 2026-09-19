using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    public sealed class AllInToAshesAchievement : HandAchievement
    {
        private const uint MinimumChips = 50000;

        public override string Id => "all_in_to_ashes";
        public override string Name => "一掷成灰";
        public override string Description => "全下前持有至少 $50,000 筹码，且全下净投入至少 $50,000，最终未赢得任何奖池。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/Ashes-v1";
        public override bool Hidden => true;
        public override int SortOrder => 309;
        public override Reward Reward => new Reward(10000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver) return;
            var player = context.Players.FirstOrDefault(value => value.AccountId == accountId);
            if (player == null || context.Pots.Any(pot => pot.Chips > 0 && pot.Winners.Contains(accountId))) return;

            foreach (var action in context.Actions.Where(value => value.AccountId == accountId &&
                value.Type == PlayerOperationType.AllIn && !value.Disconnected && value.StackBefore >= MinimumChips))
            {
                var committedAfterRefund = action.CommittedChips > player.Refund
                    ? action.CommittedChips - player.Refund
                    : 0;
                if (committedAfterRefund >= MinimumChips)
                {
                    progress.Count = Target;
                    return;
                }
            }
        }
    }
}
