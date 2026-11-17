using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The Graphics tab: resolution, window mode, quality, vSync, FPS limit. Edits the GraphicsSettingsData it
    /// is bound to (the settings screen's copy); nothing takes effect before Apply.
    /// </summary>
    public sealed class GraphicsSettingsTab : MonoBehaviour
    {
        private static readonly FullScreenMode[] WindowModes =
            { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        private static readonly string[] WindowModeKeys =
            { "settings.window_fullscreen", "settings.window_borderless", "settings.window_windowed" };
        private static readonly int[] FrameLimits = { 0, 60, 120, 144 };

        public event Action Changed;

        [SerializeField] private TMP_Dropdown _resolution;
        [SerializeField] private TMP_Dropdown _windowMode;
        [SerializeField] private TMP_Dropdown _quality;
        [SerializeField] private Toggle _vSync;
        [SerializeField] private TMP_Dropdown _frameLimit;

        private readonly List<Resolution> _resolutions = new();
        private readonly List<string> _options = new();
        private string[] _windowModeNames;
        private string[] _frameLimitNames;
        private GraphicsSettingsData _graphics;

        /// <summary>Shows these settings and edits them from now on; option lists are rebuilt here, once per open.</summary>
        public void Bind(GraphicsSettingsData graphics)
        {
            _graphics = graphics;

            _resolution.ClearOptions();
            _resolution.AddOptions(BuildResolutions());
            _resolution.SetValueWithoutNotify(FindResolution());
            _resolution.RefreshShownValue();

            EnsureNames();
            Fill(_windowMode, _windowModeNames);
            _windowMode.SetValueWithoutNotify(WindowModeIndex(graphics.windowMode));
            _windowMode.RefreshShownValue();

            Fill(_quality, QualitySettings.names);
            int quality = graphics.qualityLevel >= 0 ? graphics.qualityLevel : QualitySettings.GetQualityLevel();
            _quality.SetValueWithoutNotify(Mathf.Clamp(quality, 0, Mathf.Max(0, QualitySettings.names.Length - 1)));
            _quality.RefreshShownValue();

            _vSync.SetIsOnWithoutNotify(graphics.vSync);

            Fill(_frameLimit, _frameLimitNames);
            _frameLimit.SetValueWithoutNotify(FrameLimitIndex(graphics.frameRateLimit));
            _frameLimit.RefreshShownValue();
        }

        // Option names from UiStrings, once (Bind may come before this tab's Awake: the tab starts switched off)
        private void EnsureNames()
        {
            if (_windowModeNames != null)
                return;

            _windowModeNames = new string[WindowModeKeys.Length];
            for (int i = 0; i < WindowModeKeys.Length; i++)
                _windowModeNames[i] = UiStrings.Get(WindowModeKeys[i]);

            _frameLimitNames = new string[FrameLimits.Length];
            for (int i = 0; i < FrameLimits.Length; i++)
                _frameLimitNames[i] = FrameLimits[i] > 0 ? FrameLimits[i].ToString() : UiStrings.Get("settings.frame_limit_none");
        }

        private void OnEnable()
        {
            _resolution.onValueChanged.AddListener(HandleResolution);
            _windowMode.onValueChanged.AddListener(HandleWindowMode);
            _quality.onValueChanged.AddListener(HandleQuality);
            _vSync.onValueChanged.AddListener(HandleVSync);
            _frameLimit.onValueChanged.AddListener(HandleFrameLimit);
        }

        private void OnDisable()
        {
            _resolution.onValueChanged.RemoveListener(HandleResolution);
            _windowMode.onValueChanged.RemoveListener(HandleWindowMode);
            _quality.onValueChanged.RemoveListener(HandleQuality);
            _vSync.onValueChanged.RemoveListener(HandleVSync);
            _frameLimit.onValueChanged.RemoveListener(HandleFrameLimit);
        }

        private void HandleResolution(int index)
        {
            if (_graphics == null || index < 0 || index >= _resolutions.Count)
                return;

            Resolution resolution = _resolutions[index];
            _graphics.width = resolution.width;
            _graphics.height = resolution.height;
            _graphics.refreshRateHz = (float)resolution.refreshRateRatio.value;
            Changed?.Invoke();
        }

        private void HandleWindowMode(int index)
        {
            if (_graphics == null || index < 0 || index >= WindowModes.Length)
                return;

            _graphics.windowMode = WindowModes[index];
            Changed?.Invoke();
        }

        private void HandleQuality(int index)
        {
            if (_graphics == null)
                return;

            _graphics.qualityLevel = index;
            Changed?.Invoke();
        }

        private void HandleVSync(bool isOn)
        {
            if (_graphics == null)
                return;

            _graphics.vSync = isOn;
            Changed?.Invoke();
        }

        private void HandleFrameLimit(int index)
        {
            if (_graphics == null || index < 0 || index >= FrameLimits.Length)
                return;

            _graphics.frameRateLimit = FrameLimits[index];
            Changed?.Invoke();
        }

        // Screen.resolutions without duplicates by width × height, each with its highest refresh rate.
        private List<string> BuildResolutions()
        {
            _resolutions.Clear();
            Resolution[] all = Screen.resolutions;
            for (int i = 0; i < all.Length; i++)
                AddResolution(all[i]);

            // The editor or an odd monitor may not list the current one: the dropdown still has to show it.
            if (FindResolution() < 0)
            {
                Resolution current = Screen.currentResolution;
                if (_graphics.width > 0 && _graphics.height > 0)
                {
                    current.width = _graphics.width;
                    current.height = _graphics.height;
                }
                AddResolution(current);
            }

            _options.Clear();
            for (int i = 0; i < _resolutions.Count; i++)
                _options.Add($"{_resolutions[i].width} × {_resolutions[i].height}");
            return _options;
        }

        private void AddResolution(Resolution resolution)
        {
            for (int i = 0; i < _resolutions.Count; i++)
            {
                if (_resolutions[i].width != resolution.width || _resolutions[i].height != resolution.height)
                    continue;

                if (resolution.refreshRateRatio.value > _resolutions[i].refreshRateRatio.value)
                    _resolutions[i] = resolution;
                return;
            }
            _resolutions.Add(resolution);
        }

        // 0 × 0 in the settings = the current resolution (first start).
        private int FindResolution()
        {
            int width = _graphics.width > 0 ? _graphics.width : Screen.currentResolution.width;
            int height = _graphics.height > 0 ? _graphics.height : Screen.currentResolution.height;
            for (int i = 0; i < _resolutions.Count; i++)
            {
                if (_resolutions[i].width == width && _resolutions[i].height == height)
                    return i;
            }
            return -1;
        }

        private void Fill(TMP_Dropdown dropdown, string[] names)
        {
            _options.Clear();
            for (int i = 0; i < names.Length; i++)
                _options.Add(names[i]);
            dropdown.ClearOptions();
            dropdown.AddOptions(_options);
        }

        private static int WindowModeIndex(FullScreenMode mode)
        {
            for (int i = 0; i < WindowModes.Length; i++)
            {
                if (WindowModes[i] == mode)
                    return i;
            }
            return 2; // MaximizedWindow: shown as a window
        }

        private static int FrameLimitIndex(int limit)
        {
            for (int i = 0; i < FrameLimits.Length; i++)
            {
                if (FrameLimits[i] == limit)
                    return i;
            }
            return 0;
        }
    }
}
