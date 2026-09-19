using UnityEngine;

namespace Game.UI.Gifts
{
    public sealed class PlaceholderGiftEffect : GiftEffect
    {
        public override float Duration => 0f;
        public override void Play(RectTransform target) { }
    }
}
