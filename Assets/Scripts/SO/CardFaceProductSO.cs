using UnityEngine;

namespace Game.SO
{
    [CreateAssetMenu(fileName = "CardFace", menuName = "Poker/Card Faces/Product")]
    public sealed class CardFaceProductSO : ScriptableObject
    {
        [SerializeField] private string _id = "default";
        [SerializeField] private string _displayName = "经典牌面";
        [SerializeField, TextArea] private string _description = "熟悉的经典扑克牌风格。";
        [SerializeField] private PokerCardsSO _cards;
        [SerializeField] private uint _price;
        [SerializeField] private bool _isListed = true;
        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public PokerCardsSO Cards => _cards;
        public uint Price => _price;
        public bool IsListed => _isListed;
    }
}
