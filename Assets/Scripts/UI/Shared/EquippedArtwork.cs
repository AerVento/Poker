using System;
using System.Linq;
using Game.Content;
using Game.Network;
using Game.SO;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Holds the local account's current artwork while no room or game view is using it.</summary>
    public static class EquippedArtwork
    {
        private static readonly RemoteAssetBinding<PokerCardsSO> _cards = new RemoteAssetBinding<PokerCardsSO>();
        private static readonly RemoteAssetBinding<NamecardArtworkSO> _namecard = new RemoteAssetBinding<NamecardArtworkSO>();
        private static string _cardFaceId;
        private static string _namecardId;
        private static ItemStackMessage[] _items = Array.Empty<ItemStackMessage>();

        // Authentication supplies the card face before the private inventory snapshot arrives.
        public static void PreloadCardFace(string id)
        {
            _cardFaceId = string.IsNullOrEmpty(id) ? CardFaceDefinition.DefaultItemId : id;
            Refresh();
        }

        // Only the local account's validated private snapshots are passed here.
        public static void PreloadNamecard(InventorySnapshotMessage snapshot)
        {
            _namecardId = string.IsNullOrEmpty(snapshot.EquippedNamecard) ? NamecardDefinition.DefaultItemId : snapshot.EquippedNamecard;
            _items = snapshot.Items ?? Array.Empty<ItemStackMessage>();
            Refresh();
        }

        private static void Refresh()
        {
            var catalog = ClientCatalog.Current;
            if (!Application.isPlaying || catalog == null) return;
            if (_cardFaceId != null)
                _cards.Bind(catalog.FindCardFace(_cardFaceId)?.Cards, _ => { });
            if (_namecardId != null)
            {
                var years = catalog.ServiceYears(id => _items.FirstOrDefault(item => item.ItemId == id).Quantity);
                var visibleId = catalog.ResolveNamecard(_namecardId, years);
                _namecard.Bind(catalog.FindNamecard(visibleId)?.Artwork, _ => { });
            }
        }

        // Cancels this account's waits and releases its leases; other views and shared transfers remain independent.
        public static void Clear()
        {
            _cards.Dispose();
            _namecard.Dispose();
            _cardFaceId = null;
            _namecardId = null;
            _items = Array.Empty<ItemStackMessage>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe() => ClientCatalog.Changed += Refresh;
    }
}
