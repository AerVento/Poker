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

        private Poker _poker;
        private bool _showFront = true;

        public void SetCards(PokerCardsSO cards)
        {
            _cards = cards;
            Display(_poker, _showFront);
        }

        public virtual void Display(Poker poker, bool showFront)
        {
            _poker = poker;
            _showFront = showFront;
            _image.sprite = _cards == null ? null : showFront ? _cards.GetSprite(poker) : _cards.Back;
        }
    }
}
