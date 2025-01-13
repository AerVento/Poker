using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Framework.Input
{
    /// <summary>
    /// The button type of input. Provide two value: true and false;
    /// </summary>
    public class ButtonInput
    {
        private InputAction _action;

        /// <summary>
        /// In current frame, whether the button is pressed down.
        /// </summary>
        public bool IsPressedDown { get; private set; }

        /// <summary>
        /// In current frame, whether the button is pressed.
        /// </summary>
        public bool IsPressed => _action.IsPressed();

        /// <summary>
        /// In current frame, whether the button is released.
        /// </summary>
        public bool IsReleased { get; private set; }

        /// <summary>
        /// Called when button was pressed.
        /// </summary>
        public event System.Action OnPressed;

        /// <summary>
        /// Called when button was released.
        /// </summary>
        public event System.Action OnReleased;

        /// <summary>
        /// Use the input action to create a button input.
        /// </summary>
        /// <param name="action"></param>
        public ButtonInput(InputAction action) 
        {
            _action = action;
            
            _action.started += context =>
            {
                IsPressedDown = true;
                UniTask.Create(async () =>
                {
                    await UniTask.NextFrame();
                    IsPressedDown = false;
                });
                
                OnPressed?.Invoke();
            };

            _action.canceled += context =>
            {
                IsReleased = true;
                UniTask.Create(async () =>
                {
                    await UniTask.NextFrame();
                    IsReleased = false;
                });
                OnReleased?.Invoke();
            };
        }

        public static implicit operator bool (ButtonInput input) => input.IsPressed;
        public override string ToString()
        {
            return IsPressed.ToString();
        }
    }
}