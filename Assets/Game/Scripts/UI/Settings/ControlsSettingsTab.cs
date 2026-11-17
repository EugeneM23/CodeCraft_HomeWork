using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The Controls tab: mouse sensitivity and the key table. Keys are edited right on the game's one
    /// GameControls (the settings screen restores the old overrides on revert); sensitivity goes into the bound
    /// ControlsSettingsData. A key taken by another row of the same map is swapped, and both rows flash.
    /// Not rebindable: Look, Pause, ResetCamera, ToggleDebug and the UI map.
    /// </summary>
    public sealed class ControlsSettingsTab : MonoBehaviour
    {
        private const string WASD = "WASD";

        public event Action Changed;

        [SerializeField] private Slider _sensitivity;
        [SerializeField] private TMP_Text _sensitivityValue;
        [Tooltip("A switched-off row inside the scroll list's content; the rows are copies of it")]
        [SerializeField] private RebindRow _rowTemplate;
        [SerializeField] private Button _resetAll;
        [Tooltip("\"Press a key\" over the screen while a key is awaited")]
        [SerializeField] private GameObject _overlay;
        [SerializeField] private TMP_Text _overlayText;

        private readonly List<RebindRow> _rows = new();
        private readonly List<KeyRebindUseCase.KeySlot> _slots = new();
        private ControlsSettingsData _controls;
        private RebindRow _active;
        private int _rebindEndFrame = -1;

        public bool IsRebinding => _active != null;

        /// <summary>A rebind ended this frame: the Esc that cancelled it must not also leave the screen.</summary>
        public bool RebindEndedThisFrame => _rebindEndFrame == Time.frameCount;

        public void Bind(ControlsSettingsData controls)
        {
            _controls = controls;
            _sensitivity.SetValueWithoutNotify(controls.mouseSensitivity);
            ShowSensitivity(controls.mouseSensitivity);

            if (_rows.Count == 0)
                CreateRows();
            RefreshKeys();
            if (_overlay != null)
                _overlay.SetActive(false);
        }

        public void CancelRebind()
        {
            if (_active != null)
                _active.CancelRebind();
        }

        public void RefreshKeys()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Refresh();
        }

        private void OnEnable()
        {
            _sensitivity.onValueChanged.AddListener(HandleSensitivity);
            _resetAll.onClick.AddListener(HandleResetAll);
        }

        private void OnDisable()
        {
            _sensitivity.onValueChanged.RemoveListener(HandleSensitivity);
            _resetAll.onClick.RemoveListener(HandleResetAll);
            CancelRebind();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Rebound -= HandleRebound;
                _rows[i].RebindStarted -= HandleRebindStarted;
                _rows[i].RebindEnded -= HandleRebindEnded;
            }
        }

        private void CreateRows()
        {
            GameControls controls = AppServices.Input.Controls;
            AddPart(controls.Tank.Drive, "up", UiStrings.Get("settings.action_forward"));
            AddPart(controls.Tank.Drive, "down", UiStrings.Get("settings.action_back"));
            AddPart(controls.Tank.Drive, "left", UiStrings.Get("settings.action_left"));
            AddPart(controls.Tank.Drive, "right", UiStrings.Get("settings.action_right"));
            AddPlain(controls.Tank.Boost, UiStrings.Get("settings.action_boost"));
            AddPlain(controls.Tank.FireMain, UiStrings.Get("settings.action_fire_main"));
            AddPlain(controls.Tank.FireSecondary, UiStrings.Get("settings.action_fire_secondary"));
            AddPlain(controls.Tank.ToggleHints, UiStrings.Get("settings.action_hints"));
            AddPlain(controls.Sandbox.AirStrike, Sandbox("settings.action_air_strike"));
            AddPlain(controls.Sandbox.Smoke, Sandbox("settings.action_smoke"));
            AddPlain(controls.Sandbox.Rain, Sandbox("settings.action_rain"));
            _rowTemplate.gameObject.SetActive(false);
        }

        private static string Sandbox(string key) => string.Format(UiStrings.Get("settings.sandbox_format"), UiStrings.Get(key));

        private void AddPart(InputAction action, string part, string label) =>
            AddRow(action, KeyRebindUseCase.FindCompositePart(action, WASD, part), label);

        private void AddPlain(InputAction action, string label) =>
            AddRow(action, KeyRebindUseCase.FindFirstPlainBinding(action), label);

        private void AddRow(InputAction action, int bindingIndex, string label)
        {
            if (bindingIndex < 0)
            {
                Debug.LogWarning($"[Settings] No binding to rebind for {action.name} ({label})", this);
                return;
            }

            RebindRow row = Instantiate(_rowTemplate, _rowTemplate.transform.parent);
            row.name = label;
            row.gameObject.SetActive(true);
            row.Setup(label, action, bindingIndex);
            row.Rebound += HandleRebound;
            row.RebindStarted += HandleRebindStarted;
            row.RebindEnded += HandleRebindEnded;
            _rows.Add(row);
            _slots.Add(new KeyRebindUseCase.KeySlot(action, bindingIndex));
        }

        private void HandleRebindStarted(RebindRow row)
        {
            if (_active != null && _active != row)
                _active.CancelRebind();

            _active = row;
            if (_overlay == null)
                return;

            _overlayText.text = string.Format(UiStrings.Get("settings.rebind_wait_format"), row.Label);
            _overlay.SetActive(true);
        }

        private void HandleRebindEnded(RebindRow row)
        {
            if (_active != row)
                return;

            _active = null;
            _rebindEndFrame = Time.frameCount;
            if (_overlay != null)
                _overlay.SetActive(false);
        }

        private void HandleRebound(RebindRow row, string oldPath)
        {
            int self = _rows.IndexOf(row);
            if (self < 0)
                return;

            int other = KeyRebindUseCase.ResolveConflict(_slots, self, oldPath);
            if (other >= 0)
            {
                _rows[other].Refresh();
                _rows[other].Flash();
                row.Flash();
            }
            row.Refresh();
            Changed?.Invoke();
        }

        private void HandleResetAll()
        {
            CancelRebind();
            AppServices.Input.LoadOverrides("");
            RefreshKeys();
            Changed?.Invoke();
        }

        private void HandleSensitivity(float value)
        {
            if (_controls == null)
                return;

            _controls.mouseSensitivity = value;
            ShowSensitivity(value);
            Changed?.Invoke();
        }

        private void ShowSensitivity(float value) => _sensitivityValue.SetText("{0:1}", value); // TMP: {0:N} = N decimals
    }
}
