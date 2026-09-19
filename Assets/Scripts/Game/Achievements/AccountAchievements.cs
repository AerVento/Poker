using System;
using System.Linq;
using Game.Events;
using Game.Tasks;

namespace Game.Achievements
{
    internal static class ContractAchievementFacts
    {
        public static bool IsContract(string id) => id == "devil-contract" || id == "archdevil-contract" || id == "satan-contract";
        public static ulong Years(InventoryChangedEvent value) => InfernalNamecard.CalculateYears(
            value.Items.FirstOrDefault(i => i.ItemId == "devil-contract")?.Quantity ?? 0,
            value.Items.FirstOrDefault(i => i.ItemId == "archdevil-contract")?.Quantity ?? 0,
            value.Items.FirstOrDefault(i => i.ItemId == "satan-contract")?.Quantity ?? 0);
    }

    public abstract class EquipAppearanceAchievement : Achievement, IAchievementHandler<EquipmentChangedEvent>
    {
        protected abstract string Slot { get; }
        public override string Category => AchievementCategories.General;
        public override Reward Reward => new Reward(1000);
        public override string IconPath => "UI/Achievements/Cards-v1";

        public void Evaluate(EquipmentChangedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            if (playerEvent.AccountId == accountId && playerEvent.Slot == Slot &&
                playerEvent.ItemId != "default" && playerEvent.ItemId != playerEvent.PreviousItemId)
            {
                progress.Count = Target;
            }
        }
    }

    public sealed class EquipNewNamecardAchievement : EquipAppearanceAchievement
    {
        public override string Id => "equip_new_namecard";
        public override string Name => "锦衣加身";
        public override string Description => "换上一款新名片。";
        public override int SortOrder => 10;
        protected override string Slot => "namecard";
    }

    public sealed class EquipNewCardFaceAchievement : EquipAppearanceAchievement
    {
        public override string Id => "equip_new_card_face";
        public override string Name => "牌面担当";
        public override string Description => "换上一款新牌面。";
        public override int SortOrder => 11;
        protected override string Slot => "card-face";
    }

    public sealed class SignDevilContractAchievement : Achievement, IAchievementHandler<ItemPurchasedEvent>
    {
        public override string Id => "sign_devil_contract";
        public override string Name => "与魔为契";
        public override string Description => "与任意恶魔签订契约。";
        public override int SortOrder => 12;
        public override string IconPath => "UI/Achievements/Champion-v1";
        public override Reward Reward => new Reward(2000);

        public void Evaluate(ItemPurchasedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            if (playerEvent.AccountId == accountId && playerEvent.ProductKind == "item" &&
                playerEvent.Quantity > 0 && ContractAchievementFacts.IsContract(playerEvent.ItemId))
            {
                progress.Count = Target;
            }
        }
    }

    public sealed class RepayDevilContractAchievement : Achievement, IAchievementHandler<ItemUsedEvent>
    {
        public override string Id => "repay_devil_contract";
        public override string Name => "还债之路";
        public override string Description => "使用一份恶魔契约，偿还债务。";
        public override int SortOrder => 13;
        public override string IconPath => "UI/Achievements/Wealth-v1";
        public override Reward Reward => new Reward(2000);

        public void Evaluate(ItemUsedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            if (playerEvent.AccountId == accountId && playerEvent.Quantity > 0 && ContractAchievementFacts.IsContract(playerEvent.ItemId))
            {
                progress.Count = Target;
            }
        }
    }

    public abstract class SentenceYearsAchievement : Achievement, IAchievementHandler<InventoryChangedEvent>
    {
        public override string IconPath => "UI/Achievements/Champion-v1";

        public void Evaluate(InventoryChangedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            if (playerEvent.AccountId != accountId) return;
            var years = ContractAchievementFacts.Years(playerEvent);
            // Progress displays the highest observed current sentence, never lifetime acquired years.
            progress.Count = Math.Max(progress.Count, (int)Math.Min((ulong)Target, years));
        }
    }

    public sealed class CenturySentenceAchievement : SentenceYearsAchievement
    {
        public override string Id => "sentence_years_100";
        public override string Name => "百年之囚";
        public override string Description => "刑期达到 100 年。";
        public override int SortOrder => 14;
        public override int Target => 100;
        public override Reward Reward => new Reward(1000);
    }

    public sealed class FiveCenturySentenceAchievement : SentenceYearsAchievement
    {
        public override string Id => "sentence_years_500";
        public override string Name => "五百年劫";
        public override string Description => "刑期达到 500 年。";
        public override int SortOrder => 15;
        public override int Target => 500;
        public override Reward Reward => new Reward(2000);
    }

    public sealed class MillenniumSentenceAchievement : SentenceYearsAchievement
    {
        public override string Id => "sentence_years_1000";
        public override string Name => "千载沉沦";
        public override string Description => "刑期达到 1,000 年。";
        public override int SortOrder => 16;
        public override int Target => 1000;
        public override Reward Reward => new Reward(3000);
    }
}
