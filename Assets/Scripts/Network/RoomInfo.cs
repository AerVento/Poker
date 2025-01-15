using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;
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
        public int Id { get; set; }

        /// <summary>
        /// ����������
        /// </summary>
        public int MaxPlayerCount { get; set; } = 8;

        /// <summary>
        /// ������
        /// </summary>
        public string OwnerName { get; set; }

        /// <summary>
        /// �����
        /// </summary>
        public List<string> Players { get; set; } = new List<string>();

        /// <summary>
        /// ��ҵ�׼��״̬
        /// </summary>
        public List<bool> ReadyStatus { get; set; } = new List<bool>();

        /// <summary>
        /// ��ʼ����
        /// </summary>
        public int InitialChips { get; set; } = 300;

        /// <summary>
        /// ˼��ʱ�䣨�룩
        /// </summary>
        public float ThinkingTime { get; set; } = 30;

        /// <summary>
        /// ������Ƿ��ڵȴ���ʼ
        /// </summary>
        public bool IsWaiting { get; set; } = true;

        /// <summary>
        /// ��ǰ�������
        /// </summary>
        [JsonIgnore]
        public int PlayerCount => Players.Count;

        public static RoomInfo CreateRoom(string owner) 
        {
            RoomInfo room = new RoomInfo();
            room.OwnerName = owner;
            room.Players.Add(owner);
            return room;
        }
    }
}

