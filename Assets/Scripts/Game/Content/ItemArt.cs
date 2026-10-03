using System;
using UnityEngine;

namespace Game.Content
{
    /// <summary>
    /// Item icons and card face previews from two shared remote sprite sets. Until a set is downloaded, or when a
    /// name is missing, callers show their local default; Changed fires when real sprites become available.
    /// </summary>
    public static class ItemArt
    {
        public const string ItemIconsAddress = "icons/items";
        public const string CardFacePreviewsAddress = "previews/cardfaces";
        private static readonly Set Icons = new Set(ItemIconsAddress);
        private static readonly Set Previews = new Set(CardFacePreviewsAddress);
        public static event Action Changed;

        public static Sprite ItemIcon(string name) => Icons.Find(name);
        public static Sprite CardFacePreview(string name) => Previews.Find(name);

        private sealed class Set
        {
            private readonly string _address;
            private RemoteAssetBinding<RemoteSpriteSetSO> _binding;
            private RemoteSpriteSetSO _sprites;

            public Set(string address) => _address = address;

            public Sprite Find(string name)
            {
                if (_binding == null && Application.isPlaying)
                {
                    // One lease for the whole session; a newer version replaces it through the binding.
                    _binding = new RemoteAssetBinding<RemoteSpriteSetSO>();
                    _binding.Bind(_address, sprites =>
                    {
                        _sprites = sprites;
                        Changed?.Invoke();
                    });
                }
                return string.IsNullOrEmpty(name) || _sprites == null ? null : _sprites.Find(name);
            }

            public void Reset()
            {
                _binding?.Dispose();
                _binding = null;
                _sprites = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Icons.Reset();
            Previews.Reset();
            Changed = null;
        }
    }
}
