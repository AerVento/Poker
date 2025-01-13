using Framework.Singleton;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Framework.Input
{
    /// <summary>
    /// A manager to manager the input action asset in game. It controls the activating input action map.
    /// </summary>
    public class InputManager : MonoSingleton<InputManager>
    {
        [SerializeField]
        [Tooltip("The input action asset used to listen player input.")]
        private InputActionAsset _actions;

        [SerializeField]
        private string _initialMapName;

        private InputActionMap _current;

        /// <summary>
        /// Current input action map name.
        /// </summary>
        public string Current => _current.name;

        /// <summary>
        /// Current input action map.
        /// </summary>
        public InputActionMap CurrentMap => _current;

        /// <summary>
        /// The total input action asset.
        /// </summary>
        public InputActionAsset Actions => _actions;

        /// <summary>
        /// Find the action map by map name.
        /// </summary>
        /// <param name="mapName"></param>
        /// <returns></returns>
        public InputActionMap FindMap(string mapName) => _actions.FindActionMap(mapName);

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(this);


            if (string.IsNullOrEmpty(_initialMapName))
            {
                Debug.LogError($"Input action map name is null or empty.");
                return;
            }

            if (_actions.FindActionMap(_initialMapName) == null)
            {
                Debug.LogError($"Unknown input action map: {_initialMapName}");
                return;
            }

            ChangeMap(_initialMapName);

            OnInputActionMapChange?.Invoke(_current);
        }

        /// <summary>
        /// When the input action map changes, the manager will call this event.
        /// </summary>
        public event System.Action<InputActionMap> OnInputActionMapChange;

        /// <summary>
        /// Change the input action map to the given input action map.
        /// </summary>
        /// <param name="mapName">The name of input action map.</param>
        public void ChangeMap(string mapName)
        {
            InputActionMap newMap = _actions.FindActionMap(mapName);
            
            if (newMap != null && newMap != _current)
            {
                _current?.Disable();
                _current = newMap;
                _current.Enable();
                OnInputActionMapChange?.Invoke(newMap);
            }
        }
    }
}