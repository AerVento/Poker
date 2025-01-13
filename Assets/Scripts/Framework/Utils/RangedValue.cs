using System;
using UnityEngine;

namespace Framework.Utils
{
    /// <summary>
    /// A useful class when set up variables with range. 
    /// If a variable is lesser than minimum, its value will be set to minimum. If a variable is greater than maximum, its value will be set to maximum. 
    /// In other situations, its value will be just set to as same as given value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [System.Serializable]
    public class RangedValue<T> where T : IComparable<T>
    {
        [SerializeField]
        private T _min;

        [SerializeField]
        private T _max;
        
        [SerializeField]
        private T _value;
        public T Value
        {
            get => _value;
            set
            {
                if (value.CompareTo(_min) < 0)
                    _value = _min;
                else if (value.CompareTo(_max) > 0)
                    _value = _max;
                else
                    _value = value;
            }
        }

        public RangedValue(T min, T max)
        {
            _min = min;
            _max = max;
        }

        public RangedValue(T min, T max, T initVal) : this(min, max) 
        {
            _value = initVal;
        }

        public static implicit operator T(RangedValue<T> value) => value.Value;

        public override string ToString()
        {
            return $"{{RangeValue<{typeof(T)}>: Min={_min}, Max={_max}, Value={_value}}}";
        }
    }
}