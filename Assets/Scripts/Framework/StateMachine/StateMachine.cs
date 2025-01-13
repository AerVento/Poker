using System.Collections;
using System.Collections.Generic;

namespace Framework.States
{
    /// <summary>
    /// ״̬��
    /// </summary>
    /// <typeparam name="TKey">����״̬ʹ�õ�Key</typeparam>
    /// <typeparam name="TValue">״̬��ʵ����</typeparam>
    public class StateMachine<TKey, TValue> : IDictionary<TKey, TValue> where TValue : IState
    {
        private Dictionary<TKey, TValue> _stateDic = new Dictionary<TKey, TValue>();

        private TKey _current;

        /// <summary>
        /// ��ǰ״̬��Key
        /// </summary>
        public TKey CurrentKey
        {
            get => _current;
            set => Goto(value);
        }

        /// <summary>
        /// ��ǰ״̬��ʵ����
        /// </summary>
        public TValue CurrentState
        {
            get => _stateDic[_current];
        }

        /// <summary>
        /// ��״̬�����л�ʱ�����õ��¼�
        /// </summary>
        public event StateChangeEvent OnStateChange;

        /// <summary>
        /// ʹ��һ��״̬��Ϊ��ʼ״̬������һ��״̬���������ʼ״̬��<see cref="IState.OnEnter(StateChangeContext)"/>�������ᱻ���á�
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public StateMachine(TKey key, TValue value)
        {
            _stateDic.Add(key, value);
            _current = key;
        }

        /// <summary>
        /// ����ǰ״̬�л�Ϊ��״̬
        /// </summary>
        /// <param name="key"></param>
        public void Goto(TKey key)
        {
            if (!_stateDic.ContainsKey(key))
                throw new KeyNotFoundException($"There's no status with key {key} in current state machine.");

            IState source = _stateDic[_current];
            IState destination = _stateDic[key];
            
            StateChangeContext context = new StateChangeContext(_current, source, key, destination);
            
            source.OnExit(context);
            _current = key;
            destination.OnEnter(context);

            OnStateChange?.Invoke(context);
        }


        /// <summary>
        /// ��״̬ǿ�Ƹ�Ϊ��״̬�����������κν����˳�������
        /// </summary>
        /// <param name="key"></param>
        public void HardSet(TKey key)
        {
            _current = key;
        }

        /// <summary>
        /// ��ȡһ��״̬ʵ����ת���ɶ�Ӧ������
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T GetState<T>(TKey key) where T : IState
        {
            if (!_stateDic.ContainsKey(key))
                throw new KeyNotFoundException($"There's no status with key {key} in current state machine.");
            IState state = _stateDic[key];
            return (T)state;
        }
        #region IDictionary Implements
        public TValue this[TKey key] { get => ((IDictionary<TKey, TValue>)_stateDic)[key]; set => ((IDictionary<TKey, TValue>)_stateDic)[key] = value; }

        public ICollection<TKey> Keys => ((IDictionary<TKey, TValue>)_stateDic).Keys;

        public ICollection<TValue> Values => ((IDictionary<TKey, TValue>)_stateDic).Values;

        public int Count => ((ICollection<KeyValuePair<TKey, TValue>>)_stateDic).Count;

        public bool IsReadOnly => ((ICollection<KeyValuePair<TKey, TValue>>)_stateDic).IsReadOnly;

        /// <summary>
        /// ���һ��״̬
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            ((IDictionary<TKey, TValue>)_stateDic).Add(key, value);
        }

        /// <summary>
        /// ���һ��״̬
        /// </summary>
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)_stateDic).Add(item);
        }

        public void Clear()
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)_stateDic).Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((ICollection<KeyValuePair<TKey, TValue>>)_stateDic).Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return ((IDictionary<TKey, TValue>)_stateDic).ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)_stateDic).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<TKey, TValue>>)_stateDic).GetEnumerator();
        }

        /// <summary>
        /// �Ƴ�һ��״̬
        /// </summary>
        public bool Remove(TKey key)
        {
            return ((IDictionary<TKey, TValue>)_stateDic).Remove(key);
        }

        /// <summary>
        /// �Ƴ�һ��״̬
        /// </summary>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return ((ICollection<KeyValuePair<TKey, TValue>>)_stateDic).Remove(item);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return ((IDictionary<TKey, TValue>)_stateDic).TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_stateDic).GetEnumerator();
        }
        #endregion
    }
}