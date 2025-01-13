namespace Framework.States
{
    /// <summary>
    /// 状态接口
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// 当进入此状态时，状态机调用此函数。
        /// </summary>
        /// <param name="context">状态切换上下文</param>
        public void OnEnter(StateChangeContext context);
        
        /// <summary>
        /// 当状态机处于目前状态时，每次调用状态机的Update方法实则就是在调用当前状态的Update方法。
        /// </summary>
        public void Update();

        /// <summary>
        /// 当离开此状态时，状态机调用此函数。
        /// </summary>
        /// <param name="context">状态切换上下文</param>
        public void OnExit(StateChangeContext context);
    }


    /// <summary>
    /// 状态切换的上下文
    /// </summary>
    public class StateChangeContext
    {
        public StateChangeContext(object sourceKey, IState source, object destinationKey, IState destination)
        {
            SourceKey = sourceKey;
            Source = source;
            DestinationKey = destinationKey;
            Destination = destination;
        }

        /// <summary>
        /// 被切换掉的状态的Key
        /// </summary>
        public object SourceKey { get; private set; }

        /// <summary>
        /// 被切换掉的状态
        /// </summary>
        public IState Source { get; private set; }

        /// <summary>
        /// 即将到达的状态的Key
        /// </summary>
        public object DestinationKey { get; private set; }

        /// <summary>
        /// 即将到达的状态
        /// </summary>
        public IState Destination { get; private set; }
    }

    public delegate void StateChangeEvent(StateChangeContext context);
}