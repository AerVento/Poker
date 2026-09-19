using UnityEngine;

namespace Game.SO
{
    [CreateAssetMenu(fileName = "Item", menuName = "Poker/Items/Item Definition")]
    public sealed class ItemDefinitionSO : ScriptableObject
    {
        public const string ResourceFolder = "SO/Shop/Items/Products";

        [Tooltip("Stable inventory ID. Keep unchanged after release.")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [TextArea(2, 5)] [SerializeField] private string _description;
        [SerializeField] private Sprite _icon;
        [SerializeField] private ItemKind _kind;

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public ItemKind Kind => _kind;
    }
}
