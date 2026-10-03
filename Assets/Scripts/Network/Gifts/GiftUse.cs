namespace Game.Network.Accounts
{
    /// <summary>Upper bounds of one give request, which batches the gifts a player sends in quick succession.</summary>
    public static class GiftBatchLimits
    {
        public const int MaxCommands = 32;
        public const uint MaxEffects = 200;
    }
}
