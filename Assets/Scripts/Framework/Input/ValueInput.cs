using UnityEngine.InputSystem;

namespace Framework.Input
{
    /// <summary>
    /// The value type of input.
    /// </summary>
    /// <typeparam name="T">The type of value.</typeparam>
    public class ValueInput<T> where T : struct
    {
        private InputAction _action;

        /// <summary>
        /// In current frame, the value of input.
        /// </summary>
        public T Value => _action.ReadValue<T>();
        
        /// <summary>
        /// Use a input action to create a value input.
        /// </summary>
        /// <param name="action"></param>
        public ValueInput(InputAction action) 
        {
            _action = action;
        }

        public static implicit operator T(ValueInput<T> input) => input.Value;
        public override string ToString()
        {
            return Value.ToString();
        }
    }
}