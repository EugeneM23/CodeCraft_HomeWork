using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The debug panel: F2 (or `) opens a window with one tab per <see cref="DebugPanelRegistry"/> category and
    /// draws its entries from the prefab's row templates (built by DebugPanelBuilder in the UI pack style). Made once
    /// per game from Resources (<see cref="Create"/>), kept across levels. Allowed in the editor and development
    /// builds everywhere, in a release build only in the voxel sandbox.
    /// While open the gameplay maps are off and the cursor is free (the mouse clicks the panel, not the cannon); the
    /// game itself keeps running, so a change is seen at once. Closed, it costs one key check per frame (the FPS
    /// corner, when on, refreshes 4 times a second). Texts are re-read 4 times a second while open; the panel is
    /// a debug tool and may allocate then.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        public const string RESOURCE_PATH = "UI/DebugPanel";
        private const float REFRESH_INTERVAL = 0.25f;
        private const float FRAME_SMOOTHING = 0.05f;
        private static readonly string[] SandboxScenes = { "VoxelSandbox", "VoxelSandbox_Level" };

        [SerializeField] private GameObject _window;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private RectTransform _tabs;
        [SerializeField] private Button _tabTemplate;
        [SerializeField] private RectTransform _content;
        [SerializeField] private ScrollRect _scroll;
        [Tooltip("A row: Label (TMP) and Buttons (a horizontal layout with one template Button)")]
        [SerializeField] private RectTransform _buttonsTemplate;
        [SerializeField] private Toggle _toggleTemplate;
        [Tooltip("A row: Label (TMP) and Slider")]
        [SerializeField] private RectTransform _sliderTemplate;
        [SerializeField] private TMP_Text _infoTemplate;
        [Tooltip("The FPS line in the corner, shown with the panel closed when «FPS в углу» is on")]
        [SerializeField] private TMP_Text _fpsCorner;
        [Tooltip("Spawned by «Зомби ×5» where the level has no horde (the sandbox)")]
        [SerializeField] private GameObject _zombiePrefab;

        private static DebugPanel s_instance;

        private readonly List<DebugEntry> _entries = new();
        private readonly List<Row> _rows = new();
        private readonly List<Button> _tabButtons = new();
        private InputAction _hotkey;
        private string _category;
        private bool _dirty = true;
        private float _nextRefresh;
        private float _frameMs = 16f;
        private float _worstMs;
        private float _worstWindow;
        private bool _open;
        private bool _gameplayWasOn;
        private CursorLockMode _cursorLock;
        private bool _cursorVisible;
        private GameObject _ownEventSystem;
        private Action _onRegistryChanged;

        /// <summary>The FPS line in the corner while the panel is closed.</summary>
        public static bool ShowFpsCorner { get; set; }

        public static GameObject ZombiePrefab => s_instance != null ? s_instance._zombiePrefab : null;

        public static bool IsOpen => s_instance != null && s_instance._open;

        /// <summary>Smoothed frame time, ms (unscaled).</summary>
        public static float FrameMs => s_instance != null ? s_instance._frameMs : 0f;

        /// <summary>Editor and development builds: everywhere; a release build: only with the sandbox loaded.</summary>
        public static bool Allowed
        {
            get
            {
                if (Debug.isDebugBuild)
                    return true;

                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    string scene = SceneManager.GetSceneAt(i).name;
                    if (Array.IndexOf(SandboxScenes, scene) >= 0)
                        return true;
                }

                return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
            ShowFpsCorner = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (s_instance != null || Application.isBatchMode)
                return;

            DebugCommands.RegisterOnce();

            DebugPanel prefab = Resources.Load<DebugPanel>(RESOURCE_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"{nameof(DebugPanel)}: no Resources/{RESOURCE_PATH} (Tools/UI/Build Debug Panel)");
                return;
            }

            DebugPanel panel = Instantiate(prefab);
            panel.name = prefab.name;
            DontDestroyOnLoad(panel.gameObject);
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            _window.SetActive(false);
            _fpsCorner.gameObject.SetActive(false);
            _tabTemplate.gameObject.SetActive(false);
            _buttonsTemplate.gameObject.SetActive(false);
            _toggleTemplate.gameObject.SetActive(false);
            _sliderTemplate.gameObject.SetActive(false);
            _infoTemplate.gameObject.SetActive(false);

            _hotkey = new InputAction("DebugPanel", InputActionType.Button);
            _hotkey.AddBinding("<Keyboard>/f2");
            _hotkey.AddBinding("<Keyboard>/backquote");
            _hotkey.performed += HandleHotkey;
            _hotkey.Enable();

            _onRegistryChanged = () => _dirty = true;
            DebugPanelRegistry.Changed += _onRegistryChanged;
        }

        private void OnDestroy()
        {
            if (s_instance != this)
                return;

            if (_open)
                Close();
            _hotkey?.Dispose();
            DebugPanelRegistry.Changed -= _onRegistryChanged;
            s_instance = null;
        }

        private void HandleHotkey(InputAction.CallbackContext _)
        {
            if (_open)
                Close();
            else if (Allowed)
                Open();
        }

        // ---------- Open / close ----------

        public void Open()
        {
            if (_open)
                return;

            _open = true;
            DebugCommands.EnsureCatalogEntries();
            IGameInput input = AppServices.Input;
            _gameplayWasOn = input.Controls.Tank.enabled;
            input.SetGameplayEnabled(false);
            _cursorLock = Cursor.lockState;
            _cursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (EventSystem.current == null)
            {
                _ownEventSystem = new GameObject("DebugPanelEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                _ownEventSystem.transform.SetParent(transform, false);
            }

            _window.SetActive(true);
            _fpsCorner.gameObject.SetActive(false);
            _dirty = true;
            _nextRefresh = 0f;
        }

        public void Close()
        {
            if (!_open)
                return;

            _open = false;
            _window.SetActive(false);
            if (_ownEventSystem != null)
                Destroy(_ownEventSystem);
            _ownEventSystem = null;

            // Back as it was, unless a menu took the battle meanwhile (it owns the input and the cursor then)
            if (_gameplayWasOn)
                AppServices.Input.SetGameplayEnabled(true);
            Cursor.lockState = _cursorLock;
            Cursor.visible = _cursorVisible;
            EventSystem.current?.SetSelectedGameObject(null);
        }

        // ---------- Tick ----------

        private void Update() // perf-ok: debug panel; closed it only smooths the frame time
        {
            float dt = Time.unscaledDeltaTime * 1000f;
            _frameMs = Mathf.Lerp(_frameMs, dt, FRAME_SMOOTHING);
            _worstWindow = Mathf.Max(_worstWindow, dt);
            DebugPanelRegistry.Tick();

            if (!_open && !ShowFpsCorner)
            {
                if (_fpsCorner.gameObject.activeSelf)
                    _fpsCorner.gameObject.SetActive(false);
                return;
            }

            if (_open)
            {
                // A menu that closed meanwhile (the pause) turns the gameplay maps back on: they stay off while open
                if (AppServices.Input.Controls.Tank.enabled)
                {
                    _gameplayWasOn = true;
                    AppServices.Input.SetGameplayEnabled(false);
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }

                if (_dirty)
                    Rebuild();
            }

            float now = Time.unscaledTime;
            if (now < _nextRefresh)
                return;

            _nextRefresh = now + REFRESH_INTERVAL;
            _worstMs = _worstWindow;
            _worstWindow = 0f;

            if (_open)
                Refresh();
            else
                RefreshFpsCorner();
        }

        private void RefreshFpsCorner()
        {
            if (!_fpsCorner.gameObject.activeSelf)
                _fpsCorner.gameObject.SetActive(true);
            _fpsCorner.SetText("{0:0} FPS  {1:1} мс  макс {2:1}", 1000f / Mathf.Max(0.1f, _frameMs), _frameMs, _worstMs);
        }

        // ---------- Build ----------

        private void Rebuild()
        {
            _dirty = false;
            IReadOnlyList<string> categories = DebugPanelRegistry.Categories;
            if (_category == null || !Contains(categories, _category))
                _category = categories.Count > 0 ? categories[0] : null;

            RebuildTabs(categories);
            RebuildRows();
            Refresh();
        }

        private void RebuildTabs(IReadOnlyList<string> categories)
        {
            for (int i = 0; i < _tabButtons.Count; i++)
                Destroy(_tabButtons[i].gameObject);
            _tabButtons.Clear();

            for (int i = 0; i < categories.Count; i++)
            {
                string category = categories[i];
                Button tab = Instantiate(_tabTemplate, _tabs);
                tab.gameObject.SetActive(true);
                tab.name = "Tab_" + category;
                tab.GetComponentInChildren<TMP_Text>(true).text = category;
                Transform selected = tab.transform.Find("Selected");
                if (selected != null)
                    selected.gameObject.SetActive(category == _category);
                tab.onClick.AddListener(() => SelectCategory(category));
                _tabButtons.Add(tab);
            }
        }

        private void SelectCategory(string category)
        {
            if (category == _category)
                return;

            _category = category;
            _dirty = true;
            _scroll.verticalNormalizedPosition = 1f;
        }

        private void RebuildRows()
        {
            for (int i = 0; i < _rows.Count; i++)
                Destroy(_rows[i].Root);
            _rows.Clear();

            if (_category == null)
                return;

            DebugPanelRegistry.Collect(_category, _entries);
            for (int i = 0; i < _entries.Count; i++)
                _rows.Add(CreateRow(_entries[i]));
        }

        private Row CreateRow(DebugEntry entry)
        {
            var row = new Row { Entry = entry };
            switch (entry.Kind)
            {
                case DebugEntryKind.Buttons:
                {
                    RectTransform rect = Instantiate(_buttonsTemplate, _content);
                    row.Root = rect.gameObject;
                    row.Label = rect.Find("Label").GetComponent<TMP_Text>();
                    Transform buttons = rect.Find("Buttons");
                    Button template = buttons.GetChild(0).GetComponent<Button>();
                    int count = entry.Buttons?.Length ?? 0;
                    row.Buttons = new Button[count];
                    row.ButtonLabels = new TMP_Text[count];
                    for (int b = 0; b < count; b++)
                    {
                        Button button = b == 0 ? template : Instantiate(template, buttons);
                        int index = b;
                        button.onClick.AddListener(() => Press(entry, index));
                        row.Buttons[b] = button;
                        row.ButtonLabels[b] = button.GetComponentInChildren<TMP_Text>(true);
                        row.ButtonLabels[b].text = entry.Buttons[b];
                    }

                    if (count == 0)
                        template.gameObject.SetActive(false);
                    if (entry.Label == null)
                        row.Label.gameObject.SetActive(false);
                    break;
                }
                case DebugEntryKind.Toggle:
                {
                    Toggle toggle = Instantiate(_toggleTemplate, _content);
                    row.Root = toggle.gameObject;
                    row.Toggle = toggle;
                    row.Label = toggle.GetComponentInChildren<TMP_Text>(true);
                    toggle.SetIsOnWithoutNotify(Read(entry.GetToggle));
                    toggle.onValueChanged.AddListener(v => Run(() => entry.SetToggle?.Invoke(v)));
                    break;
                }
                case DebugEntryKind.Slider:
                {
                    RectTransform rect = Instantiate(_sliderTemplate, _content);
                    row.Root = rect.gameObject;
                    row.Label = rect.Find("Label").GetComponent<TMP_Text>();
                    row.Slider = rect.GetComponentInChildren<Slider>(true);
                    row.Slider.minValue = entry.Min;
                    row.Slider.maxValue = entry.Max;
                    row.Slider.SetValueWithoutNotify(ReadValue(entry));
                    row.Slider.onValueChanged.AddListener(v => Run(() => entry.SetValue?.Invoke(v)));
                    break;
                }
                default:
                {
                    TMP_Text text = Instantiate(_infoTemplate, _content);
                    row.Root = text.gameObject;
                    row.Label = text;
                    break;
                }
            }

            row.Root.name = entry.Kind + "_" + _rows.Count;
            row.Root.SetActive(true);
            return row;
        }

        // ---------- Refresh ----------

        private void Refresh()
        {
            _status.SetText("{0:0} FPS   {1:1} мс   макс {2:1} мс   время ×{3:2}", 1000f / Mathf.Max(0.1f, _frameMs), _frameMs,
                _worstMs, Time.timeScale);

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                DebugEntry entry = row.Entry;
                bool visible = entry.IsVisible;
                if (row.Root.activeSelf != visible)
                    row.Root.SetActive(visible);
                if (!visible)
                    continue;

                if (entry.Label != null && row.Label != null)
                {
                    string label = entry.ReadLabel();
                    if (entry.Kind == DebugEntryKind.Slider)
                        label = $"{label}: {ReadValue(entry):0.##}";
                    if (row.Label.text != label)
                        row.Label.text = label;
                }

                if (row.Toggle != null)
                    row.Toggle.SetIsOnWithoutNotify(Read(entry.GetToggle));

                if (row.Slider != null && !IsDragging(row.Slider))
                    row.Slider.SetValueWithoutNotify(ReadValue(entry));

                if (row.Buttons != null && entry.ButtonText != null)
                {
                    for (int b = 0; b < row.Buttons.Length; b++)
                    {
                        string text = entry.ReadButton(b);
                        bool shown = !string.IsNullOrEmpty(text);
                        if (row.Buttons[b].gameObject.activeSelf != shown)
                            row.Buttons[b].gameObject.SetActive(shown);
                        if (shown && row.ButtonLabels[b].text != text)
                            row.ButtonLabels[b].text = text;
                    }
                }
            }
        }

        private void Press(DebugEntry entry, int index)
        {
            Run(() => entry.Pressed?.Invoke(index));
            _nextRefresh = 0f; // show the result at once
        }

        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static bool Read(Func<bool> get)
        {
            try
            {
                return get != null && get();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static float ReadValue(DebugEntry entry)
        {
            try
            {
                return entry.GetValue != null ? entry.GetValue() : entry.Min;
            }
            catch (Exception)
            {
                return entry.Min;
            }
        }

        private static bool IsDragging(Slider slider) =>
            EventSystem.current != null && EventSystem.current.currentSelectedGameObject == slider.gameObject &&
            Mouse.current != null && Mouse.current.leftButton.isPressed;

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                    return true;
            }

            return false;
        }

        private sealed class Row
        {
            public DebugEntry Entry;
            public GameObject Root;
            public TMP_Text Label;
            public Button[] Buttons;
            public TMP_Text[] ButtonLabels;
            public Toggle Toggle;
            public Slider Slider;
        }
    }
}
