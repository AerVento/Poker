using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Cysharp.Threading.Tasks;
using Framework.Singleton;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Framework.UI
{
    public class UIManager : MonoSingleton<UIManager>
    {
        #region Initialization

        [Header("Initialization")]
        [SerializeField]
        private GameObject _canvasPrefab;

        [SerializeField]
        private GameObject _eventSystemPrefab;

        [Header("Settings")]
        // Available panels
        [SerializeField]
        private GameObject[] _panels;
        #endregion

        #region Runtime Fields
        // canvas instance & event system instance
        private Canvas _canvas;
        private EventSystem _eventSystem;

        // four layers for panels to show
        private Transform _bottomLayer;
        private Transform _middleLayer;
        private Transform _topLayer;
        private Transform _systemLayer;

        // Runtime Panel Dictionary, managing loaded panel prefabs.
        private Dictionary<Type, GameObject> _runtimePanels;

        // Runtime Panel HashSet, managing showing panel component instance. 
        private Dictionary<string, GameObject> _showingPanels = new();
        #endregion

        /// <summary>
        /// The current canvas.
        /// </summary>
        public Canvas Canvas => _canvas;

        /// <summary>
        /// The current event system.
        /// </summary>
        public EventSystem EventSystem => _eventSystem;

        /// <summary>
        /// Identifiers of all showing panel.
        /// </summary>
        public ICollection<string> ShowingPanelIdentifiers => _showingPanels.Keys;

        protected override void Awake()
        {
            base.Awake();
            InitializeEnvironment();
            InitializeRuntimePanel();
        }

        private void InitializeEnvironment()
        {
            DontDestroyOnLoad(gameObject);

            _canvas = Instantiate(_canvasPrefab, transform).GetComponent<Canvas>();
            _eventSystem = Instantiate(_eventSystemPrefab, transform).GetComponent<EventSystem>();

            _bottomLayer = _canvas.transform.Find("Bot");
            _middleLayer = _canvas.transform.Find("Mid");
            _topLayer = _canvas.transform.Find("Top");
            _systemLayer = _canvas.transform.Find("System");
        }

        private void InitializeRuntimePanel()
        {
            _runtimePanels = new();
            foreach (var panel in _panels)
            {
                if (panel.TryGetComponent(out IPanel component))
                {
                    // if the type already exists
                    if (!_runtimePanels.TryAdd(component.GetType(), panel))
                    {
                        Debug.LogWarning($"Adding Panel Error: On GameObject {panel}, Type {component.GetType()} already exists. ");
                    }
                }
                else
                {
                    Debug.LogWarning($"GameObject {panel} doesn't have a component which derived from IPanel.");
                }
            }
        }
        
        /// <summary>
        /// Get layer transform.
        /// </summary>
        public Transform GetLayer(Layer layer)
        {
            return layer switch
            {
                Layer.Bottom => _bottomLayer,
                Layer.Middle => _middleLayer,
                Layer.Top => _topLayer,
                Layer.System => _systemLayer,
                _ => null
            };
        }

        #region Panel LifeCycles
        
        /// <summary>
        /// Get a existing panel instance.
        /// </summary>
        /// <typeparam name="T">The type of panel.</typeparam>
        /// <param name="identifier">The identifier of the panel to get.</param>
        /// <returns>The panel component.</returns>
        public T GetPanel<T>(string identifier) where T : IPanel
        {
            if (_showingPanels.TryGetValue(identifier, out var target))
            {
                return target.GetComponent<T>();
            }
            else
                throw new KeyNotFoundException($"The panel identifier {identifier} doesn't exists.");
        }


        /// <summary>
        /// Get a existing panel instance using the class name as the panel identifier.
        /// </summary>
        /// <typeparam name="T">The type of panel.</typeparam>
        /// <returns>>The panel component.</returns>
        public T GetPanel<T>() where T : SingletonPanel<T>, IPanel
        {
            Type t = typeof(T);
            return GetPanel<T>(t.Name);
        }

        /// <summary>
        /// Instantiate a panel and set a identifier for it. The identifier must be unique.
        /// </summary>
        /// <param name="type">The panel type.</param>
        public IPanel CreatePanel(Type type, string identifier, Layer layer = Layer.Middle)
        {
            if (!_runtimePanels.TryGetValue(type, out GameObject prefab))
            {
                Debug.LogWarning("Unknown panel type: " + type);
                return null;
            }

            if (_showingPanels.ContainsKey(identifier))
            {
                Debug.LogError("Panel identifier already exists: " + identifier);
                return null;
            }

            GameObject instance = Instantiate(prefab, GetLayer(layer));
            _showingPanels.Add(identifier, instance);

            IPanel panel = instance.GetComponent<IPanel>();
            panel.Identifier = identifier;
            return panel;
        }

        /// <summary>
        /// Instantiate a panel and set a identifier for it. The identifier must be unique.
        /// </summary>
        public T CreatePanel<T>(string identifer, Layer layer = Layer.Middle) where T : IPanel
        {
            return (T)CreatePanel(typeof(T), identifer, layer);
        }

        /// <summary>
        /// Instantiate a panel and use its class name as the panel identifer.
        /// Notice that you can only create one instance per time in this way.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="layer"></param>
        /// <returns></returns>
        public T CreatePanel<T>(Layer layer = Layer.Middle) where T : SingletonPanel<T>, IPanel
        {
            Type type = typeof(T);
            return (T)CreatePanel(type, type.Name, layer);
        }

        /// <summary>
        /// Destroy a panel.
        /// </summary>
        /// <param name="identifier">The identifier of the panel to destroy.</param>
        public void DestroyPanel(string identifier)
        {
            if (_showingPanels.TryGetValue(identifier, out GameObject target))
            {
                Destroy(target);
                _showingPanels.Remove(identifier);
            }
        }

        #endregion

        #region Show Panel

        /// <summary>
        /// Show a panel. If the panel doesn't exists, it will be created.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="identifier"></param>
        public void ShowPanel<T>(string identifier, Layer layer = Layer.Middle) where T : IPanel
        {
            if (!_showingPanels.ContainsKey(identifier))
                CreatePanel<T>(identifier, layer).Show();
            else
                GetPanel<T>(identifier).Show();
        }

        /// <summary>
        /// Show a panel. If the panel doesn't exists, it will be created.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="identifier"></param>
        public void ShowPanel<T>(Layer layer = Layer.Middle) where T : SingletonPanel<T>, IPanel
        {
            Type type = typeof(T);
            if (!_showingPanels.ContainsKey(type.Name))
                CreatePanel(type, type.Name, layer).Show();
            else
                GetPanel<T>(type.Name).Show();
        }

        /// <summary>
        /// Show a panel asynchronizly. If the panel doesn't exists, it will be created.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="identifier"></param>
        public async UniTask ShowPanelAsync<T>(string identifier, Layer layer = Layer.Middle) where T : IAsyncPanel
        {
            if (!_showingPanels.ContainsKey(identifier))
                await CreatePanel<T>(identifier, layer).ShowAsync();
            else
                await GetPanel<T>(identifier).ShowAsync();
        }

        /// <summary>
        /// Show a panel asynchronizly. If the panel doesn't exists, it will be created.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="identifier"></param>
        public async UniTask ShowPanelAsync<T>(Layer layer = Layer.Middle) where T : SingletonPanel<T>, IAsyncPanel
        {
            Type type = typeof(T);
            if (!_showingPanels.ContainsKey(type.Name))
                await ((IAsyncPanel)CreatePanel(type, type.Name, layer)).ShowAsync();
            else
                await GetPanel<T>(type.Name).ShowAsync();
        }

        #endregion

        #region Hide Panel
        /// <summary>
        /// Hide a panel. If the panel doesn't exists, it will do nothing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="identifier"></param>
        public void HidePanel<T>(string identifier) where T : IPanel
        {
            if (_showingPanels.ContainsKey(identifier))
                GetPanel<T>(identifier).Hide();
        }

        /// <summary>
        /// Hide a panel. If the panel doesn't exists, it will do nothing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void HidePanel<T>() where T : SingletonPanel<T>, IPanel
        {
            Type type = typeof(T);
            if (_showingPanels.ContainsKey(type.Name))
                GetPanel<T>(type.Name).Hide();
        }

        /// <summary>
        /// Hide a panel asynchronizly. If the panel doesn't exists, it will do nothing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="identifier"></param>
        public async UniTask HidePanelAsync<T>(string identifier) where T : IAsyncPanel
        {
            if (_showingPanels.ContainsKey(identifier)) 
                await GetPanel<T>(identifier).HideAsync();
        }

        /// <summary>
        /// Hide a panel asynchronizly. If the panel doesn't exists, it will do nothing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="identifier"></param>
        public async UniTask HidePanelAsync<T>() where T : SingletonPanel<T>, IAsyncPanel
        {
            Type type = typeof(T);
            if (_showingPanels.ContainsKey(type.Name))
                await GetPanel<T>(type.Name).HideAsync();
        }

        #endregion

        public enum Layer
        {
            Bottom,
            Middle,
            Top,
            System
        }
    }
}
