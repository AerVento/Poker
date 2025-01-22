using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// 形态
    /// </summary>
    [System.Serializable]
    public struct PokerPattern
    {
        /// <summary>
        /// 牌型
        /// </summary>
        public PokerHandRank Rank { get; set; }

        /// <summary>
        /// 当牌型相同时，使用等级来比较哪个大
        /// 下标越小处的等级优先级越高，比较大小时优先考虑下标小的等级
        /// 当一个下标处的等级一样时，再看下一个优先级
        /// </summary>
        public List<int> Grades { get; set; }

        public bool BiggerThan(PokerPattern other)
        {
            if (Rank != other.Rank)
                return Rank > other.Rank;
            else
            {
                for(int i = 0; i < Grades.Count; i++)
                {
                    var gradeA = Grades[i];
                    var gradeB = other.Grades[i];
                    if(gradeA != gradeB)
                        return gradeA > gradeB;
                }
                // 比到最后是相等的，返回false
                return false;
            }
        }

        public bool BiggerThanOrSame(PokerPattern other)
        {
            if (Rank != other.Rank)
                return Rank > other.Rank;
            else
            {
                for (int i = 0; i < Grades.Count; i++)
                {
                    var gradeA = Grades[i];
                    var gradeB = other.Grades[i];
                    if (gradeA != gradeB)
                        return gradeA > gradeB;
                }
                // 比到最后是相等的，true
                return true;
            }
        }

        public bool SmallerThan(PokerPattern other)
        {
            if (Rank != other.Rank)
                return Rank < other.Rank;
            else
            {
                for (int i = 0; i < Grades.Count; i++)
                {
                    var gradeA = Grades[i];
                    var gradeB = other.Grades[i];
                    if (gradeA != gradeB)
                        return gradeA < gradeB;
                }
                // 比到最后是相等的，返回false
                return false;
            }
        }

        public bool SmallerThanOrSame(PokerPattern other)
        {
            if (Rank != other.Rank)
                return Rank < other.Rank;
            else
            {
                for (int i = 0; i < Grades.Count; i++)
                {
                    var gradeA = Grades[i];
                    var gradeB = other.Grades[i];
                    if (gradeA != gradeB)
                        return gradeA < gradeB;
                }
                // 比到最后是相等的，true
                return true;
            }
        }

        public bool SameTo(PokerPattern other)
        {
            if (Rank != other.Rank)
                return false;
            else
            {
                for (int i = 0; i < Grades.Count; i++)
                {
                    var gradeA = Grades[i];
                    var gradeB = other.Grades[i];
                    if (gradeA != gradeB)
                        return false;
                }
                // 比到最后是相等的，true
                return true;
            }
        }

        public override string ToString()
        {
            string rank = Rank switch
            {
                PokerHandRank.HighCard => "高牌",
                PokerHandRank.Pair => "一对",
                PokerHandRank.Two_Pair => "两对",
                PokerHandRank.Three_of_a_Kind => "三条",
                PokerHandRank.Straight => "顺子",
                PokerHandRank.Flush => "同花",
                PokerHandRank.Full_House => "葫芦",
                PokerHandRank.Four_of_a_Kind => "四条",
                PokerHandRank.Straight_Flush => "同花顺",
                _ => "未知牌型"
            };
            if (Rank == PokerHandRank.Straight_Flush && Grades[0] == (int)PokerRank.Ace)
                rank = "皇家同花顺";
            return rank;
        }
    }
}
