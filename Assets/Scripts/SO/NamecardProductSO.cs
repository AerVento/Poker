using UnityEngine;

namespace Game.SO
{
    [CreateAssetMenu(fileName = "NamecardProduct", menuName = "Poker/Shop/Namecard Product")]
    public sealed class NamecardProductSO : ScriptableObject
    {
        [Tooltip("Stable inventory ID. Do not change after this product has been sold.")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [TextArea(2, 4)]
        [SerializeField] private string _description;
        [Tooltip("Wide 8:3 artwork; also used by the shop preview.")]
        [SerializeField] private Sprite _artwork;
        [Tooltip("Separately composed 4:3 artwork for taller namecard slots.")]
        [SerializeField] private Sprite _artwork4x3;
        [Tooltip("Price in existing account chips. The server must validate purchases.")]
        [SerializeField] private uint _price = 6000;

        [Tooltip("Always owned; no purchase or unlock is required.")]
        [SerializeField] private bool _isDefault;

        [SerializeField] private bool _isListed = true;

        [Tooltip("When disabled, a listed card remains visible with a disabled purchase button.")]
        [SerializeField] private bool _canPurchase = true;

        [Tooltip("Achievement reward: cannot be purchased, but can be equipped after claiming.")]
        [SerializeField] private bool _isReward;

        public bool IsReward => !_isDefault && _isReward;
        public bool CanPurchase => _isDefault || (!_isReward && _canPurchase);
        public bool IsListed => _isDefault || _isListed;
        public bool IsDefault => _isDefault;
        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Artwork => _artwork;
        public Sprite Artwork4x3 => _artwork4x3;

        public Sprite GetArtwork(float targetAspect)
        {
            if (_artwork == null)
                return _artwork4x3;
            if (_artwork4x3 == null || _isDefault || targetAspect <= 0f ||
                float.IsNaN(targetAspect) || float.IsInfinity(targetAspect))
                return _artwork;

            // Compare relative aspect differences so either variant fits with minimal empty space.
            float wideAspect = _artwork.rect.width / _artwork.rect.height;
            float compactAspect = _artwork4x3.rect.width / _artwork4x3.rect.height;
            return Mathf.Abs(Mathf.Log(targetAspect / compactAspect)) <
                Mathf.Abs(Mathf.Log(targetAspect / wideAspect)) ? _artwork4x3 : _artwork;
        }
        public uint Price => _isDefault ? 0 : _price;
    }
}
