using Cysharp.Threading.Tasks;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace Framework.Mono
{
    /// <summary>
    /// �����ϵĹ���Monoģ�������
    /// </summary>
    public class MonoController : MonoBehaviour
    {
        private CancellationTokenSource _cancel = new CancellationTokenSource(); // ��MonoController���ݻ�ʱ��GameLogicˢ�±�ȡ��

        /// <summary>
        /// ��ÿ��֡����ʱ������õ��¼�
        /// </summary>
        public event System.Action OnUpdate;

        /// <summary>
        /// ��ÿ������֡����ʱ������õ��¼�
        /// </summary>
        public event System.Action OnFixedUpdate;

        /// <summary>
        /// ��ÿ����Ϸ�߼�����ʱ����õ��¼�
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