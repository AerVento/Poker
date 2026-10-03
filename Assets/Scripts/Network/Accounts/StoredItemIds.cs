namespace Game.Network.Accounts
{
    /// <summary>Reads every item ID any account owns, to find inventory the item catalog no longer defines.</summary>
    public interface IStoredItemRepository
    {
        string[] ReadStoredItemIds();
    }
}
