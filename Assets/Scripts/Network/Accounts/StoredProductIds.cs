using System;

namespace Game.Network.Accounts
{
    /// <summary>Every product ID any account owns, to find inventory the shop configuration no longer defines.</summary>
    public sealed class StoredProductIds
    {
        public string[] Items { get; set; } = Array.Empty<string>();
        public string[] Namecards { get; set; } = Array.Empty<string>();
        public string[] CardFaces { get; set; } = Array.Empty<string>();
    }

    public interface IStoredProductRepository
    {
        StoredProductIds ReadStoredProductIds();
    }
}
