using Framework.Singleton;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.SO
{
    /// <summary>
    /// 卡牌图案信息资源库
    /// </summary>
    [CreateAssetMenu(fileName = "PokerCardsSO", menuName = "SO/PokerCardSO")]
    public class PokerCardsSO : SingletonSO
    {
        [SerializeField]
        private List<Item> _items;

        [SerializeField]
        private Sprite _back;

        /// <summary>
        /// 卡牌背面图案
        /// </summary>
        public Sprite Back => _back;

        /// <summary>
        /// 某张卡牌的正面图案
        /// </summary>
        /// <param name="poker"></param>
        /// <returns></returns>
        public Sprite GetSprite(Poker poker)
        {
            foreach(var item in _items)
            {
                if (item.Suit == poker.Suit && item.Rank == poker.Rank)
                    return item.Front;
            }
            return null;
        }

        [Serializable]
        struct Item
        {
            public PokerSuit Suit;
            public PokerRank Rank;
            public Sprite Front;
        }
    }
}
