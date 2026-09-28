namespace Game.Achievements
{
    /// <summary>Completes when the player equips a different non-default namecard.</summary>
    public sealed class EquipNewNamecardAchievement : EquipAppearanceAchievement
    {
        public override string Id => "equip_new_namecard";
        public override string Name => "锦衣加身";
        public override string Description => "换上一款新名片。";
        public override int SortOrder => 10;
        protected override string Slot => "namecard";
    }
}
