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
        /// shows them for a while. Not in a tick: builds a string.
        /// </summary>
        public void RefreshHints()
        {
            GameControls.TankActions tank = Controls.Tank;
            _hintsText.text =
                tank.FireMain.GetBindingDisplayString() + " — Пушка\n" +
                tank.FireSecondary.GetBindingDisplayString() + " — Пулемёт\n" +
                tank.Boost.GetBindingDisplayString() + " — Форсаж\n" +
                tank.Drive.GetBindingDisplayString(0) + " — Езда\n" +
                tank.Pause.GetBindingDisplayString() + " — Пауза\n" +
                tank.ToggleHints.GetBindingDisplayString() + " — скрыть";
        }

        private GameControls Controls => _controls ??= AppServices.Input.Controls;

        private void Awake()
        {
            _onToggleHints = HandleToggleHints;
            _onToggleDebug = HandleToggleDebug;
            _debug.SetActive(false);
        }

        // The stack shows the HUD when the battle starts (the object is off until then): fresh hints.
        private void OnEnable()
        {
            Controls.Tank.ToggleHints.performed += _onToggleHints;
            Controls.Tank.ToggleDebug.performed += _onToggleDebug;
            _nextRefresh = 0f;
            RefreshHints();
            ShowHints(false);
        }

        private void OnDisable()
        {
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
                    _killsText.SetText("Regax: {0:0}/{1:0}", kills, target);
                else
                    _killsText.SetText("Regax: {0:0}", kills);
            }

            int seconds = ClockTextUseCase.ShownSeconds(_stats.ElapsedTime);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                ClockTextUseCase.Split(seconds, out int minutes, out int rest);
                _timeText.SetText("Время {0:00}:{1:00}", minutes, rest);
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
            _fpsText.SetText("FPS {0:0}   кадр {1:1} мс", 1000f / Mathf.Max(0.1f, _frameMs), _frameMs);
            VoxelDebugStatsUseCase.Read(out VoxelDebugStats stats);
            _voxelsText.SetText("Воксели: {0:0} в {1:0} телах ({2:0} кусков)", stats.Voxels, stats.Bodies, stats.LoosePieces);
            _debrisText.SetText("Обломки: {0:0}", stats.Debris);
            _remeshText.SetText("Перестройка: {0:0} чанков за {1:1} мс ({2:0} в работе)",
                stats.RemeshChunks, stats.RemeshMs, stats.AsyncInFlight);
            _smokeText.SetText("Дым: {0:0} облаков, {1:0} кубов", stats.SmokeClouds, stats.SmokeCubes);
        }
    }
}
