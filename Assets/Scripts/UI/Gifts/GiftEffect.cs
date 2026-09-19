using UnityEngine;

namespace Game.UI.Gifts
{
    [RequireComponent(typeof(RectTransform))]
    public abstract class GiftEffect : MonoBehaviour
    {
        public abstract float Duration { get; }
        public abstract void Play(RectTransform target);
    }
}
