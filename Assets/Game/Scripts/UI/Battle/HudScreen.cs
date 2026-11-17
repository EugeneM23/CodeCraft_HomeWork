using System;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The battle HUD, root of the battle stack. The tank's bars and crosshair are TankHudView's; here are
    /// the battle counters (IBattleStats), the key hints (current bindings, fade out after a while, F1)
    /// and the debug block (F3: FPS and the voxel world's numbers, formerly VoxelDemoHud). Text is
    /// refreshed at most 4 times a second or on change, numbers through SetText: no allocations per frame.
    /// </summary>
    public sealed class HudScreen : UIScreen
    {
        private const float REFRESH_INTERVAL = 0.25f;
        private const float HINTS_SHOWN_FOR = 15f;
        private const float HINTS_FADE = 1f;
        private const float FRAME_SMOOTHING = 0.05f;

        [SerializeField] private TankHudView _tank;

        [Header("Battle counters")]
        [SerializeField] private TMP_Text _killsText;
        [SerializeField] private TMP_Text _timeText;

        [Header("Hints")]
        [SerializeField] private CanvasGroup _hints;
        [SerializeField] private TMP_Text _hintsText;

        [Header("Debug (F3)")]
        [SerializeField] private GameObject _debug;
        [SerializeField] private TMP_Text _fpsText;
        [SerializeField] private TMP_Text _voxelsText;
        [SerializeField] private TMP_Text _debrisText;
        [SerializeField] private TMP_Text _remeshText;
        [SerializeField] private TMP_Text _smokeText;

        private IBattleStats _stats;
        private GameControls _controls;
        private Action<InputAction.CallbackContext> _onToggleHints;
        private Action<InputAction.CallbackContext> _onToggleDebug;

        private float _nextRefresh;
        private int _shownKills = -1;
        private int _shownTarget = -1;
        private int _shownSeconds = -1;
        private float _frameMs = 16f;

        private bool _hintsVisible;
        private bool _hintsPinned;
        private float _hintsShownAt;

        // Formats from UiStrings, fetched once in Awake (TMP SetText formats: {0:1} = one decimal)
        private string _hintsFormat;
        private string _killsTargetFormat;
        private string _killsFormat;
        private string _timeFormat;
        private string _fpsFormat;
        private string _voxelsFormat;
        private string _debrisFormat;
        private string _remeshFormat;
        private string _smokeFormat;

        /// <summary>The battle counters shown top-left; null hides them.</summary>
        public void SetStats(IBattleStats stats)
        {
            _stats = stats;
            _killsText.gameObject.SetActive(stats != null);
            _timeText.gameObject.SetActive(stats != null);
            _shownKills = _shownTarget = _shownSeconds = -1;
        }

        /// <summary>
        /// Rebuilds the hint lines from the current bindings (they may have been changed in the settings) and
        /// shows them for a while. Key names by physical key (KeyNameUseCase). Not in a tick: builds a string.
        /// </summary>
        public void RefreshHints()
        {
            GameControls.TankActions tank = Controls.Tank;
            _hintsText.text = string.Format(_hintsFormat,
                KeyNameUseCase.OfAction(tank.FireMain),
                KeyNameUseCase.OfAction(tank.FireSecondary),
                KeyNameUseCase.OfAction(tank.Boost),
                KeyNameUseCase.OfBinding(tank.Drive, 0),
                KeyNameUseCase.OfAction(tank.Pause),
                KeyNameUseCase.OfAction(tank.ToggleHints));
        }

        private GameControls Controls => _controls ??= AppServices.Input.Controls;

        private void Awake()
        {
            _hintsFormat = UiStrings.Get("hud.hints_format");
            _killsTargetFormat = UiStrings.Get("hud.kills_target_format");
            _killsFormat = UiStrings.Get("hud.kills_format");
            _timeFormat = UiStrings.Get("hud.time_format");
            _fpsFormat = UiStrings.Get("hud.debug_fps_format");
            _voxelsFormat = UiStrings.Get("hud.debug_voxels_format");
            _debrisFormat = UiStrings.Get("hud.debug_debris_format");
            _remeshFormat = UiStrings.Get("hud.debug_remesh_format");
            _smokeFormat = UiStrings.Get("hud.debug_smoke_format");

            _onToggleHints = HandleToggleHints;
            _onToggleDebug = HandleToggleDebug;
            _debug.SetActive(false);
        }

        // The stack shows the HUD when the battle starts (the object is off until then): fresh hints.
        private void OnEnable()
        {
            VoxelSandboxOrbit.CrosshairByHud = true;
            Controls.Tank.ToggleHints.performed += _onToggleHints;
            Controls.Tank.ToggleDebug.performed += _onToggleDebug;
            _nextRefresh = 0f;
            RefreshHints();
            ShowHints(false);
        }

        private void OnDisable()
        {
            VoxelSandboxOrbit.CrosshairByHud = false;
            Controls.Tank.ToggleHints.performed -= _onToggleHints;
            Controls.Tank.ToggleDebug.performed -= _onToggleDebug;
        }

        protected override void Update() // perf-ok: ticks during pause (hud)
        {
            base.Update();

            float now = Time.unscaledTime;
            _frameMs = Mathf.Lerp(_frameMs, Time.unscaledDeltaTime * 1000f, FRAME_SMOOTHING);
            _tank.Tick();
            TickHints(now);

            if (now < _nextRefresh)
                return;

            _nextRefresh = now + REFRESH_INTERVAL;
            RefreshStats();
            if (_debug.activeSelf)
                RefreshDebug();
        }

        // ---------- Counters ----------

        private void RefreshStats()
        {
            if (_stats == null)
                return;

            int kills = _stats.Kills;
            int target = _stats.KillTarget;
            if (kills != _shownKills || target != _shownTarget)
            {
                _shownKills = kills;
                _shownTarget = target;
                if (target > 0)
                    _killsText.SetText(_killsTargetFormat, kills, target);
                else
                    _killsText.SetText(_killsFormat, kills);
            }

            int seconds = ClockTextUseCase.ShownSeconds(_stats.ElapsedTime);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                ClockTextUseCase.Split(seconds, out int minutes, out int rest);
                _timeText.SetText(_timeFormat, minutes, rest);
            }
        }

        // ---------- Hints ----------

        private void HandleToggleHints(InputAction.CallbackContext _)
        {
            if (_hintsVisible)
                HideHints();
            else
                ShowHints(true);
        }

        /// <summary>Pinned (asked for with F1): stays until F1 again; otherwise fades out after a while.</summary>
        private void ShowHints(bool pinned)
        {
            _hintsVisible = true;
            _hintsPinned = pinned;
            _hintsShownAt = Time.unscaledTime;
            _hints.alpha = 1f;
            _hints.gameObject.SetActive(true);
        }

        private void HideHints()
        {
            _hintsVisible = false;
            _hints.gameObject.SetActive(false);
        }

        private void TickHints(float now)
        {
            if (!_hintsVisible || _hintsPinned)
                return;

            float fade = (now - _hintsShownAt - HINTS_SHOWN_FOR) / HINTS_FADE;
            if (fade <= 0f)
                return;

            if (fade >= 1f)
                HideHints();
            else
                _hints.alpha = 1f - fade;
        }

        // ---------- Debug ----------

        private void HandleToggleDebug(InputAction.CallbackContext _)
        {
            bool show = !_debug.activeSelf;
            _debug.SetActive(show);
            if (show)
                RefreshDebug();
        }

        private void RefreshDebug()
        {
            _fpsText.SetText(_fpsFormat, 1000f / Mathf.Max(0.1f, _frameMs), _frameMs);
            VoxelDebugStatsUseCase.Read(out VoxelDebugStats stats);
            _voxelsText.SetText(_voxelsFormat, stats.Voxels, stats.Bodies, stats.LoosePieces);
            _debrisText.SetText(_debrisFormat, stats.Debris);
            _remeshText.SetText(_remeshFormat, stats.RemeshChunks, stats.RemeshMs, stats.AsyncInFlight);
            _smokeText.SetText(_smokeFormat, stats.SmokeClouds, stats.SmokeCubes);
        }
    }
}
