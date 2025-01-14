using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Network
{
    /// <summary>
    /// ������Ϣ
    /// </summary>
    [System.Serializable]
    public class RoomInfo
    {
        /// <summary>
        /// �������
        /// </summary>
        public int Id { get; private set; }
        
        /// <summary>
        /// ��ǰ�������
        /// </summary>
        public int PlayerCount => PlayerNames.Count;

        /// <summary>
        /// ����������
        /// </summary>
        public int MaxPlayerCount { get; private set; }

        /// <summary>
        /// ������
        /// </summary>
        public string OwnerName { get; private set; }

        /// <summary>
        /// �����
        /// </summary>
        public List<string> PlayerNames { get; private set; } = new List<string>();

        public RoomInfo(string owner, int maxPlayer) 
        {
            OwnerName = owner;
            MaxPlayerCount = maxPlayer;
            PlayerNames.Add(owner);
        }
    }
}

