using Cysharp.Threading.Tasks;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace Framework.Mono
{
    /// <summary>
    /// 场景上的公共Mono模块控制器
    /// </summary>
    public class MonoController : MonoBehaviour
    {
        private CancellationTokenSource _cancel = new CancellationTokenSource(); // 当MonoController被摧毁时，GameLogic刷新被取消

        /// <summary>
        /// 在每次帧更新时都会调用的事件
        /// </summary>
        public event System.Action OnUpdate;

        /// <summary>
        /// 在每次物理帧更新时都会调用的事件
        /// </summary>
        public event System.Action OnFixedUpdate;

        /// <summary>
        /// 在每次游戏逻辑更新时会调用的事件
        /// </summary>
        public event System.Action OnGameLogicUpdate;

        private void Start()
        {
            UniTask.Void(async (token) =>
            {
                while(!token.IsCancellationRequested)
                {
                    OnGameLogicUpdate?.Invoke();
                    await UniTask.Delay(MonoManager.GameLogicDeltaTime, false, PlayerLoopTiming.Update, token);
                }
            }, _cancel.Token);   
        }

        private void Update()
        {
            OnUpdate?.Invoke();
        }

        private void FixedUpdate()
        {
            OnFixedUpdate?.Invoke();       
        }
    }
}