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
        private sealed class PanelEntry
        {
            public string Identifier;
            public Layer Layer;
            public CanvasGroup Input;
            public bool Interactable;
            public bool BlocksRaycasts;
            public GameObject PreviousSelection;
        }

        // Bottom to top; removing an entry preserves the order of all remaining panels.
        private readonly List<PanelEntry> _panelOrder = new();
        #endregion

        public string TopPanelIdentifier => _panelOrder.Count == 0 ? null : _panelOrder[_panelOrder.Count - 1].Identifier;

        public IReadOnlyList<string> PanelOrder => _panelOrder.ConvertAll(entry => entry.Identifier).AsReadOnly();

        public bool ContainsPanel(string identifier) => _panelOrder.Exists(entry => entry.Identifier == identifier);

        public bool ContainsPanel<T>() where T : SingletonPanel<T> => ContainsPanel(typeof(T).Name);

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
                // Remove ownership before Hide: transient panels may destroy themselves in Hide.
                _showingPanels.Remove(identifier);
                RemoveFromOrder(identifier);
                if (target != null)
                {
                    target.GetComponent<IPanel>().Hide();
                    var panel = target.GetComponent<BasePanel>();
                    if (panel != null)
                        panel.ReleaseManagedInstance();
                    if (Application.isPlaying)
                        Destroy(target);
                    else
                        DestroyImmediate(target);
                }
            }
        }

        #endregion

        #region Panel Navigation

        /// <summary>
        /// Open or bring a panel forward within its render layer. Other panels stay active.
        /// </summary>
        public T PushPanel<T>(string identifier, Layer layer = Layer.Middle) where T : IPanel
        {
            var panel = _showingPanels.ContainsKey(identifier)
                ? GetPanel<T>(identifier) : CreatePanel<T>(identifier, layer);
            if (panel == null)
                return default;
            TrackPanel(identifier, true);
            panel.Show();
            return panel;
        }

        public T PushPanel<T>(Layer layer = Layer.Middle) where T : SingletonPanel<T>, IPanel
            => PushPanel<T>(typeof(T).Name, layer);

        /// <summary>Close the topmost panel, or a specified panel at any depth.</summary>
        public bool PopPanel() => PopPanel(TopPanelIdentifier);

        public bool PopPanel(string identifier)
        {
            if (string.IsNullOrEmpty(identifier) || !_showingPanels.TryGetValue(identifier, out var target))
                return false;
            bool removed = RemoveFromOrder(identifier);
            if (target != null)
                target.GetComponent<IPanel>().Hide();
            return removed;
        }

        public bool PopPanel<T>() where T : SingletonPanel<T>, IPanel => PopPanel(typeof(T).Name);

        public bool PopPanel<T>(string identifier) where T : IPanel => PopPanel(identifier);

        /// <summary>Discard navigation history, dismissing panels without invoking button callbacks.</summary>
        public void ClearPanels()
        {
            var identifiers = PanelOrder;
            for (int i = identifiers.Count - 1; i >= 0; i--)
                PopPanel(identifiers[i]);
        }

        public T ResetPanels<T>(Layer layer = Layer.Middle) where T : SingletonPanel<T>, IPanel
        {
            ClearPanels();
            return PushPanel<T>(layer);
        }

        // BasePanel also reports direct Show/Hide/Destroy calls, so history cannot retain a closed panel.
        internal void NotifyPanelShown(IPanel panel)
        {
            if (OwnsPanel(panel))
                TrackPanel(panel.Identifier, false);
        }

        internal void NotifyPanelHidden(IPanel panel)
        {
            if (OwnsPanel(panel))
                RemoveFromOrder(panel.Identifier);
        }

        internal void NotifyPanelDestroyed(IPanel panel)
        {
            if (!OwnsPanel(panel))
                return;
            RemoveFromOrder(panel.Identifier);
            _showingPanels.Remove(panel.Identifier);
        }

        private bool OwnsPanel(IPanel panel) => !string.IsNullOrEmpty(panel.Identifier) &&
            _showingPanels.TryGetValue(panel.Identifier, out var target) &&
            target != null && ReferenceEquals(target.GetComponent<IPanel>(), panel);

        private void TrackPanel(string identifier, bool bringForward)
        {
            if (_showingPanels[identifier].GetComponent<BasePanel>() is BasePanel panel && !panel.ParticipatesInNavigation)
                return;
            int existing = _panelOrder.FindIndex(entry => entry.Identifier == identifier);
            if (existing >= 0 && !bringForward)
                return;
            var target = _showingPanels[identifier];
            PanelEntry entry;
            if (existing >= 0)
            {
                entry = _panelOrder[existing];
                _panelOrder.RemoveAt(existing);
            }
            else
            {
                var input = target.GetComponent<CanvasGroup>();
                if (input == null)
                    input = target.AddComponent<CanvasGroup>();
                var parent = target.transform.parent;
                entry = new PanelEntry
                {
                    Identifier = identifier,
                    Layer = parent == _systemLayer ? Layer.System : parent == _topLayer ? Layer.Top :
                        parent == _bottomLayer ? Layer.Bottom : Layer.Middle,
                    Input = input,
                    Interactable = input.interactable,
                    BlocksRaycasts = input.blocksRaycasts,
                    PreviousSelection = _eventSystem == null ? null : _eventSystem.currentSelectedGameObject,
                };
            }
            int index = _panelOrder.FindIndex(other => other.Layer > entry.Layer);
            _panelOrder.Insert(index < 0 ? _panelOrder.Count : index, entry);
            target.transform.SetAsLastSibling();
            RefreshPanelInput();
        }

        private bool RemoveFromOrder(string identifier)
        {
            int index = _panelOrder.FindIndex(entry => entry.Identifier == identifier);
            if (index < 0)
                return false;
            bool wasTop = index == _panelOrder.Count - 1;
            var entry = _panelOrder[index];
            _panelOrder.RemoveAt(index);
            if (entry.Input != null)
            {
                entry.Input.interactable = entry.Interactable;
                entry.Input.blocksRaycasts = entry.BlocksRaycasts;
            }
            RefreshPanelInput();
            if (wasTop && _eventSystem != null)
            {
                var selection = entry.PreviousSelection;
                bool valid = selection != null && selection.activeInHierarchy &&
                    TopPanelIdentifier != null && _showingPanels.TryGetValue(TopPanelIdentifier, out var top) &&
                    selection.transform.IsChildOf(top.transform);
                _eventSystem.SetSelectedGameObject(valid ? selection : null);
            }
            return true;
        }

        private void RefreshPanelInput()
        {
            // Keep covered panels alive, while preventing clicks/tab navigation through the foreground UI.
            for (int i = 0; i < _panelOrder.Count; i++)
            {
                var entry = _panelOrder[i];
                if (entry.Input == null)
                    continue;
                bool isTop = i == _panelOrder.Count - 1;
                entry.Input.interactable = isTop && entry.Interactable;
                entry.Input.blocksRaycasts = isTop && entry.BlocksRaycasts;
                var selected = _eventSystem == null ? null : _eventSystem.currentSelectedGameObject;
                if (!isTop && selected != null && selected.transform.IsChildOf(entry.Input.transform))
                    _eventSystem.SetSelectedGameObject(null);
            }
        }

        // Compatibility entry points delegate to navigation; game code uses PushPanel/PopPanel.
        public void ShowPanel<T>(string identifier, Layer layer = Layer.Middle) where T : IPanel
            => PushPanel<T>(identifier, layer);

        public void ShowPanel<T>(Layer layer = Layer.Middle) where T : SingletonPanel<T>, IPanel
            => PushPanel<T>(layer);

        public void HidePanel<T>(string identifier) where T : IPanel => PopPanel(identifier);

        public void HidePanel<T>() where T : SingletonPanel<T>, IPanel => PopPanel<T>();

        public async UniTask PushPanelAsync<T>(string identifier, Layer layer = Layer.Middle) where T : IAsyncPanel
        {
            var panel = _showingPanels.ContainsKey(identifier)
                ? GetPanel<T>(identifier) : CreatePanel<T>(identifier, layer);
            if (panel == null)
                return;
            TrackPanel(identifier, true);
            await panel.ShowAsync();
            // A transition can remove the panel while its opening animation is still running.
            if (!ContainsPanel(identifier) && OwnsPanel(panel))
                panel.Hide();
        }

        public UniTask PushPanelAsync<T>(Layer layer = Layer.Middle) where T : SingletonPanel<T>, IAsyncPanel
            => PushPanelAsync<T>(typeof(T).Name, layer);

        public async UniTask PopPanelAsync<T>(string identifier) where T : IAsyncPanel
        {
            if (!_showingPanels.TryGetValue(identifier, out var target) || target == null)
                return;
            RemoveFromOrder(identifier);
            await target.GetComponent<T>().HideAsync();
        }

        public UniTask PopPanelAsync<T>() where T : SingletonPanel<T>, IAsyncPanel
            => PopPanelAsync<T>(typeof(T).Name);

        public UniTask ShowPanelAsync<T>(string identifier, Layer layer = Layer.Middle) where T : IAsyncPanel
            => PushPanelAsync<T>(identifier, layer);

        public UniTask ShowPanelAsync<T>(Layer layer = Layer.Middle) where T : SingletonPanel<T>, IAsyncPanel
            => PushPanelAsync<T>(layer);

        public UniTask HidePanelAsync<T>(string identifier) where T : IAsyncPanel => PopPanelAsync<T>(identifier);

        public UniTask HidePanelAsync<T>() where T : SingletonPanel<T>, IAsyncPanel => PopPanelAsync<T>();

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
