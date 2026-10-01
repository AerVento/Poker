using UnityEngine;

namespace Game.Content
{
    [CreateAssetMenu(fileName = "NamecardArtwork", menuName = "Poker/Content/Namecard Artwork")]
    public sealed class NamecardArtworkSO : ScriptableObject
    {
        [SerializeField] private Sprite _wide;
        [SerializeField] private Sprite _compact;
        public Sprite Wide => _wide;
        public Sprite Compact => _compact;

        public Sprite Select(float aspect) => Select(_wide, _compact, aspect);

        public static Sprite Select(Sprite wide, Sprite compact, float aspect)
        {
            if (wide == null) return compact;
            if (compact == null || aspect <= 0 || float.IsNaN(aspect) || float.IsInfinity(aspect)) return wide;
            return Mathf.Abs(Mathf.Log(aspect / (compact.rect.width / compact.rect.height))) <
                Mathf.Abs(Mathf.Log(aspect / (wide.rect.width / wide.rect.height))) ? compact : wide;
        }
    }
}
