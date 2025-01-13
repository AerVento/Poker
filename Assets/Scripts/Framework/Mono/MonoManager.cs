using UnityEngine;

namespace Framework.Mono
{
    /// <summary>
    /// ����Monoģ��Ŀ������Ĺ�����
    /// </summary>
    public static class MonoManager
    {
        /// <summary>
        /// ��Ϸ�߼�֡ˢ��ʱ�䣨���룩
        /// </summary>
        public const int GameLogicDeltaTime = 20;

        private static MonoController _controller;
        private static MonoController _Controller
        {
            get
            {
                if(_controller == null)
                {
                    GameObject obj = new GameObject("MonoController");
                    _controller = obj.AddComponent<MonoController>();
                    
                    // ���ﲻ��DontDestroyOnLoad��ԭ���ǣ����س���ʱ������Monoģ������֮ǰ�����ļ���������Щ�������������ѱ�ɾ����
                    // ��ˣ�����ֱ�ӽ�MonoController���ų����ļ���һ��ɾ������ֹ�������⡣
                }
                return _controller;
            }
        }

        /// <summary>
        /// �������Ƿ��п�����
        /// </summary>
        public static bool IsControllerExists => _controller != null;

        /// <summary>
        /// ��ÿ��֡����ʱ������õ��¼�
        /// </summary>
        public static event System.Action OnUpdate
        {
            add => _Controller.OnUpdate += value;
            remove => _Controller.OnUpdate -= value;
        }

        /// <summary>
        /// ��ÿ������֡����ʱ������õ��¼�
        /// </summary>
        public static event System.Action OnFixedUpdate
        {
            add => _Controller.OnFixedUpdate += value;
            remove => _Controller.OnFixedUpdate -= value;
        }

        /// <summary>
        /// ÿ����Ϸ�߼�֡����ʱ����
        /// </summary>
        public static event System.Action OnGameLogicUpdate
        {
            add => _Controller.OnGameLogicUpdate += value;
            remove => _Controller.OnGameLogicUpdate -= value;
        }
    }
}