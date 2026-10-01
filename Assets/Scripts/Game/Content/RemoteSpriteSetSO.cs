using System;
using UnityEngine;

namespace Game.Content
{
    /// <summary>A remote bundle of small sprites looked up by name: all item icons, or all card face previews.</summary>
    [CreateAssetMenu(fileName = "SpriteSet", menuName = "Poker/Content/Remote Sprite Set")]
    public sealed class RemoteSpriteSetSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _name;
            [SerializeField] private Sprite _sprite;
            public string Name => _name;
            public Sprite Sprite => _sprite;

            public Entry(string name, Sprite sprite)
            {
                _name = name;
                _sprite = sprite;
            }
        }

        [SerializeField] private Entry[] _sprites = Array.Empty<Entry>();
        public Entry[] Sprites => _sprites;

        public Sprite Find(string spriteName)
        {
            foreach (var entry in _sprites)
                if (entry != null && entry.Name == spriteName) return entry.Sprite;
            return null;
        }
    }
}
