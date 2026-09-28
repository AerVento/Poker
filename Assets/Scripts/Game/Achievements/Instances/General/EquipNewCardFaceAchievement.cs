namespace Game.Achievements
{
    /// <summary>Completes when the player equips a different non-default card face.</summary>
    public sealed class EquipNewCardFaceAchievement : EquipAppearanceAchievement
    {
        public override string Id => "equip_new_card_face";
        public override string Name => "牌面担当";
        public override string Description => "换上一款新牌面。";
        public override int SortOrder => 11;
        protected override string Slot => "card-face";
    }
}
