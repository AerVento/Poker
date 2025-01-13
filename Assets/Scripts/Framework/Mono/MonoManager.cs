using UnityEngine;

namespace Framework.Mono
{
    /// <summary>
    /// 公有Mono模块的控制器的管理者
    /// </summary>
    public static class MonoManager
    {
        /// <summary>
        /// 游戏逻辑帧刷新时间（毫秒）
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
                    
                    // 这里不加DontDestroyOnLoad的原因是，加载场景时，公有Mono模块会存在之前场景的监听，而那些监听的物体早已被删除。
                    // 因此，这里直接将MonoController随着场景的加载一起删除，防止出现问题。
                }
                return _controller;
            }
        }

        /// <summary>
        /// 场景上是否有控制器
        /// </summary>
        public static bool IsControllerExists => _controller != null;

        /// <summary>
        /// 在每次帧更新时都会调用的事件
        /// </summary>
        public static event System.Action OnUpdate
        {
            add => _Controller.OnUpdate += value;
            remove => _Controller.OnUpdate -= value;
        }

        /// <summary>
        /// 在每次物理帧更新时都会调用的事件
        /// </summary>
        public static event System.Action OnFixedUpdate
        {
            add => _Controller.OnFixedUpdate += value;
            remove => _Controller.OnFixedUpdate -= value;
        }

        /// <summary>
        /// 每次游戏逻辑帧更新时调用
        /// </summary>
        public static event System.Action OnGameLogicUpdate
        {
            add => _Controller.OnGameLogicUpdate += value;
            remove => _Controller.OnGameLogicUpdate -= value;
        }
    }
}