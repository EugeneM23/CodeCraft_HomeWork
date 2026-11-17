using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// One line of the key table: the action's name, its key on a button and a reset button. A click on the key
    /// waits for a new one (PerformInteractiveRebinding); Esc cancels. While it waits every map of the asset is
    /// off, so the key pressed does not also click the menu or fire the tank. Conflicts are solved by the tab.
    /// </summary>
    public sealed class RebindRow : MonoBehaviour
    {
        private const float FLASH_SECONDS = 0.8f;
        private const float MATCH_WAIT_SECONDS = 0.1f;
        private const string WAITING_TEXT = "…";

        /// <summary>The key changed (a rebind finished or the row was reset); the string is the key it had before.</summary>
        public event Action<RebindRow, string> Rebound;

        /// <summary>A rebind started on this row.</summary>
        public event Action<RebindRow> RebindStarted;

        /// <summary>The rebind ended, by a key or by Esc.</summary>
        public event Action<RebindRow> RebindEnded;

        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Button _keyButton;
        [SerializeField] private TMP_Text _keyText;
        [SerializeField] private Button _resetButton;

        private InputAction _action;
        private int _bindingIndex = -1;
        private InputActionRebindingExtensions.RebindingOperation _operation;
        private bool[] _mapsWereEnabled;
        private string _pathBefore;
        private Color _backgroundColor;
        private float _flashLeft;

        public InputAction Action => _action;
        public int BindingIndex => _bindingIndex;
        public string Label => _label.text;
        public bool IsRebinding => _operation != null;

        public void Setup(string label, InputAction action, int bindingIndex)
        {
            _label.text = label;
            _action = action;
            _bindingIndex = bindingIndex;
            if (_background != null)
                _backgroundColor = _background.color;
            Refresh();
        }

        public void Refresh()
        {
            if (_action == null || _bindingIndex < 0)
                return;

            _keyText.text = _action.GetBindingDisplayString(_bindingIndex);
        }

        /// <summary>Lights the row up for a moment: its key was taken by another row and swapped.</summary>
        public void Flash() => _flashLeft = FLASH_SECONDS;

        public void StartRebind()
        {
            if (_operation != null || _action == null || _bindingIndex < 0)
                return;

            _pathBefore = _action.bindings[_bindingIndex].effectivePath;
            DisableMaps();
            _keyText.text = WAITING_TEXT;

            _operation = _action.PerformInteractiveRebinding(_bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Pointer>/position")
                .OnMatchWaitForAnother(MATCH_WAIT_SECONDS)
                .OnComplete(HandleComplete)
                .OnCancel(HandleCancel);

            RebindStarted?.Invoke(this);
            _operation.Start();
        }

        public void CancelRebind() => _operation?.Cancel();

        /// <summary>Back to the asset's key; the tab then swaps out a row that holds the same key.</summary>
        public void ResetToDefault()
        {
            if (_action == null || _bindingIndex < 0 || _operation != null)
                return;

            string before = _action.bindings[_bindingIndex].effectivePath;
            _action.RemoveBindingOverride(_bindingIndex);
            Refresh();
            Rebound?.Invoke(this, before);
        }

        private void OnEnable()
        {
            _keyButton.onClick.AddListener(StartRebind);
            _resetButton.onClick.AddListener(ResetToDefault);
        }

        private void OnDisable()
        {
            _keyButton.onClick.RemoveListener(StartRebind);
            _resetButton.onClick.RemoveListener(ResetToDefault);
            CancelRebind();
            StopFlash();
        }

        private void Update() // perf-ok: ticks during pause (settings menu row flash)
        {
            if (_flashLeft <= 0f || _background == null)
                return;

            _flashLeft -= Time.unscaledDeltaTime;
            if (_flashLeft <= 0f)
            {
                StopFlash();
                return;
            }
            _background.color = Color.Lerp(_backgroundColor, UiStyle.Accent, _flashLeft / FLASH_SECONDS);
        }

        private void HandleComplete(InputActionRebindingExtensions.RebindingOperation operation)
        {
            string before = _pathBefore;
            Finish();
            Rebound?.Invoke(this, before);
        }

        private void HandleCancel(InputActionRebindingExtensions.RebindingOperation operation) => Finish();

        private void Finish()
        {
            _operation?.Dispose();
            _operation = null;
            RestoreMaps();
            Refresh();
            RebindEnded?.Invoke(this);
        }

        // The rebind needs the action's map off; the other maps go off too, so the key does nothing else.
        private void DisableMaps()
        {
            ReadOnlyArray<InputActionMap> maps = _action.actionMap.asset.actionMaps;
            if (_mapsWereEnabled == null || _mapsWereEnabled.Length != maps.Count)
                _mapsWereEnabled = new bool[maps.Count];

            for (int i = 0; i < maps.Count; i++)
            {
                _mapsWereEnabled[i] = maps[i].enabled;
                maps[i].Disable();
            }
        }

        private void RestoreMaps()
        {
            ReadOnlyArray<InputActionMap> maps = _action.actionMap.asset.actionMaps;
            for (int i = 0; i < maps.Count && i < _mapsWereEnabled.Length; i++)
            {
                if (_mapsWereEnabled[i])
                    maps[i].Enable();
            }
        }

        private void StopFlash()
        {
            _flashLeft = 0f;
            if (_background != null && _action != null)
                _background.color = _backgroundColor;
        }
    }
}
