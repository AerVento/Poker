namespace Framework.States
{
    /// <summary>
    /// ״̬�ӿ�
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// �������״̬ʱ��״̬�����ô˺�����
        /// </summary>
        /// <param name="context">״̬�л�������</param>
        public void OnEnter(StateChangeContext context);
        
        /// <summary>
        /// ��״̬������Ŀǰ״̬ʱ��ÿ�ε���״̬����Update����ʵ������ڵ��õ�ǰ״̬��Update������
        /// </summary>
        public void Update();

        /// <summary>
        /// ���뿪��״̬ʱ��״̬�����ô˺�����
        /// </summary>
        /// <param name="context">״̬�л�������</param>
        public void OnExit(StateChangeContext context);
    }


    /// <summary>
    /// ״̬�л���������
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
        /// ���л�����״̬��Key
        /// </summary>
        public object SourceKey { get; private set; }

        /// <summary>
        /// ���л�����״̬
        /// </summary>
        public IState Source { get; private set; }

        /// <summary>
        /// ���������״̬��Key
        /// </summary>
        public object DestinationKey { get; private set; }

        /// <summary>
        /// ���������״̬
        /// </summary>
        public IState Destination { get; private set; }
    }

    public delegate void StateChangeEvent(StateChangeContext context);
}