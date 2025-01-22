using Framework.Log;
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
        private static bool CreateController()
        {
            try
            {
                GameObject obj = new GameObject("MonoController");
                _controller = obj.AddComponent<MonoController>();
                // ���ﲻ��DontDestroyOnLoad��ԭ���ǣ����س���ʱ������Monoģ������֮ǰ�����ļ���������Щ�������������ѱ�ɾ����
                // ��ˣ�����ֱ�ӽ�MonoController���ų����ļ���һ��ɾ������ֹ�������⡣
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                _controller = null;
                return false;
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
            add 
            {
                if(_controller == null)
                {
                    if (!CreateController())
                        return;
                }
                _controller.OnUpdate += value;
            }
            remove
            {
                if (_controller == null)
                    return;
                _controller.OnUpdate -= value;
            }
        }

        /// <summary>
        /// ��ÿ������֡����ʱ������õ��¼�
        /// </summary>
        public static event System.Action OnFixedUpdate
        {
            add
            {
                if (_controller == null)
                {
                    if (!CreateController())
                        return;
                }
                _controller.OnFixedUpdate += value;
            }
            remove
            {
                if (_controller == null)
                    return;
                _controller.OnFixedUpdate -= value;
            }
        }

        /// <summary>
        /// ÿ����Ϸ�߼�֡����ʱ����
        /// </summary>
        public static event System.Action OnGameLogicUpdate
        {
            add
            {
                if (_controller == null)
                {
                    if (!CreateController())
                        return;
                }
                _controller.OnGameLogicUpdate += value;
            }
            remove
            {
                if (_controller == null)
                    return;
                _controller.OnGameLogicUpdate -= value;
            }
        }
    }
}