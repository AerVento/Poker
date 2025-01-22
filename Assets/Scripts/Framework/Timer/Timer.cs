using Cysharp.Threading.Tasks;
using Framework.Mono;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Framework.Timer
{
    /// <summary>
    /// A timer has boolean value to notice if the timer was time up. And the timer can be start and stopped.
    /// It has a constant duration to count down.
    /// </summary>
    public class Timer
    {
        private bool _isCounting;
        private double _time = 0;
        private double _duration = 0;

        /// <summary>
        /// Whether the timer is counting down.
        /// </summary>
        public bool IsCounting => _isCounting;

        /// <summary>
        /// The time passed since the count down started in seconds.
        /// </summary>
        public double TimePassed
        {
            get => _isCounting ? _time : 0;
        }

        /// <summary>
        /// The time remaining before time up in seconds.
        /// </summary>
        public double TimeRemaining
        {
            get => _isCounting ? _duration - _time : 0;
        }

        /// <summary>
        /// The duration of timer.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Thrown when try to set duration when the timer was counting down.</exception>
        /// <exception cref="System.ArgumentException">Thrown when try to set a negative number to the duration.</exception>
        public double Duration
        {
            get => _duration;
            set
            {
                if (_isCounting)
                    throw new System.InvalidOperationException("Cannot change the duration while the timer was counting down.");
                if (value <= 0)
                    throw new System.ArgumentException($"The duration of timer must be a positive number. Given: {value}.", "value");

                _duration = value;
            }

        }

        /// <summary>
        /// If the timer uses the scaled time.
        /// </summary>
        public bool UseScaledTime { get; set; } = true;

        /// <summary>
        /// When time up, this event will be automatically called. It will not be called if the timer was stopped by extern.
        /// </summary>
        public System.Action OnTimeUp;

        /// <summary>
        /// Initialize a timer with a default duration (1 second).
        /// </summary>
        public Timer() : this(1) { }

        /// <summary>
        /// Intialize a timer with a duration(seconds).
        /// </summary>
        /// <param name="duration">The duration in seconds.</param>
        public Timer(double duration)
        {
            Duration = duration;
        }

        ~Timer()
        {
            MonoManager.OnUpdate -= InternalUpdate; 
        }

        /// <summary>
        /// A internal update function which called every frame update.
        /// </summary>
        private void InternalUpdate()
        {
            if (!_isCounting)
                return;
            _time += UseScaledTime ? Time.deltaTime : Time.unscaledDeltaTime;
            if (_time > _duration)
            {
                // 这里手动停止一遍是防止调用委托时整个计时器仍显示出“正在计时状态”而导致未知后果
                Stop();
                OnTimeUp?.Invoke();
            }
        }

        /// <summary>
        /// Let the timer to start count down.
        /// </summary>
        public void Start()
        {
            if (_isCounting)
                return;

            _isCounting = true;
            MonoManager.OnUpdate += InternalUpdate;
        }

        /// <summary>
        /// A async method which start the timer and returns when the timer is time up.
        /// </summary>
        /// <returns>Returns true if the timer completed the count down. Otherwise, false.</returns>
        public async UniTask<bool> StartAsync()
        {
            if (!_isCounting)
            {
                _isCounting = true;
                MonoManager.OnUpdate += InternalUpdate;
            }

            // FIXME: 现在使用了每一帧都去获取是否结束，希望使用Awaiter，等待结束的时候异步的返回
            bool finished = false;
            OnTimeUp += TimeUpFunc;
            while (_isCounting)
                await UniTask.NextFrame();
            return finished;

            void TimeUpFunc()
            {
                finished = true;
                OnTimeUp -= TimeUpFunc;
            }
        }

        /// <summary>
        /// Pause the timer.
        /// </summary>
        public void Pause()
        {
            if (!_isCounting)
                return;

            _isCounting = false;
            MonoManager.OnUpdate -= InternalUpdate;
        }

        /// <summary>
        /// Stop the timer from counting down.
        /// </summary>
        public void Stop()
        {
            if (!_isCounting)
                return;

            _time = 0;
            _isCounting = false;
            MonoManager.OnUpdate -= InternalUpdate;
        }

        /// <summary>
        /// Set time passed to 0 and restart to count down.
        /// </summary>
        public void Clear()
        {
            _time = 0;
        }

        public override string ToString()
        {
            if(!IsCounting)
                return $"{{Timer: IsCounting=false, UseScaleTime={UseScaledTime}, Duration={Duration}}}";
            else
                return $"{{Timer: IsCounting=true, UseScaleTime={UseScaledTime}, Duration={Duration}, TimeRemaining={TimeRemaining}}}";
        }

    }
}