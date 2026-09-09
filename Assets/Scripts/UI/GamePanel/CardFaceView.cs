using Game.SO;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // Visual-only prefab contract. No room state, ownership, or network components belong here.
    public class CardFaceView : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private PokerCardsSO _cards;
        public PokerCardsSO Cards => _cards;
        public Image Image => _image;

        public virtual void Display(Poker poker, bool showFront)
        {
            _image.sprite = showFront ? _cards.GetSprite(poker) : _cards.Back;
        }
    }
}
