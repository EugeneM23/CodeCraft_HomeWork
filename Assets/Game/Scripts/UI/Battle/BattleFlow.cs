using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The battle UI's orchestrator, on the BattleUi root. One screen stack: the HUD at the root, the pause
    /// over it (settings and Yes/No questions above that), the results in place of the HUD at the end.
    /// While anything covers the HUD the game is paused, the gameplay maps (Tank, Sandbox) are off and the
    /// cursor is free; back on the bare HUD the game runs, the tank drives and the cursor is locked.
    /// Esc is both Tank.Pause and UI.Cancel: Pause opens the pause only from the HUD and not in the frame the
    /// pause closed, Cancel goes back only when the HUD is not on top and not in the frame the pause opened,
    /// so one press does one thing. While the game flow loads a scene, keys and buttons do nothing.
    /// </summary>
    public sealed class BattleFlow : MonoBehaviour
    {
        private const string HANGAR_SCREEN = "Hangar";
        private const string OLD_SANDBOX_HUD = "VoxelDemoHud";

        [SerializeField] private HudScreen _hud;
        [SerializeField] private PauseScreen _pause;
        [SerializeField] private BattleResultsScreen _results;
        [SerializeField] private ConfirmDialog _confirmDialog;
        [SerializeField] private SettingsScreen _settings;
        [SerializeField] private LevelCatalog _levelCatalog;
        [Tooltip("Implements IBattleStats and IBattleResultSource (SandboxBattleStats in VoxelSandbox)")]
        [SerializeField] private MonoBehaviour _battleStats;

        private ScreenFlow _flow;
        private IGameInput _input;
        private IBattleResultSource _resultSource;
        private LevelConfig _nextLevel;
        private int _pauseOpenedFrame = -1;
        private int _pauseClosedFrame = -1;
        private bool _gameStopped;
        private bool _finished;

        private Action<InputAction.CallbackContext> _onPauseKey;
        private Action<InputAction.CallbackContext> _onCancel;
        private Action<BattleResult> _onFinished;

        private void Awake()
        {
            AppServices.RegisterLevels(_levelCatalog);
            _input = AppServices.Input;
            _flow = new ScreenFlow(_confirmDialog);
            _settings.Bind(_flow);

            _hud.SetStats(_battleStats as IBattleStats);
            _resultSource = _battleStats as IBattleResultSource;
            if (_battleStats != null && _resultSource == null)
                Debug.LogWarning($"{nameof(BattleFlow)}: {_battleStats.GetType().Name} is not an IBattleResultSource, the battle never ends.", this);

            _onPauseKey = HandlePauseKey;
            _onCancel = HandleCancel;
            _onFinished = HandleFinished;
        }

        private void OnEnable()
        {
            _input.Controls.Tank.Pause.performed += _onPauseKey;
            _input.Controls.UI.Cancel.performed += _onCancel;
            _flow.Stack.Changed += HandleStackChanged;
            if (_resultSource != null)
                _resultSource.Finished += _onFinished;

            _pause.ResumeClicked += HandleResume;
            _pause.SettingsClicked += HandleSettings;
            _pause.RestartClicked += HandleRestart;
            _pause.MenuClicked += HandleMenu;
            _pause.QuitClicked += HandleQuit;
            _results.AgainClicked += HandleAgain;
            _results.HangarClicked += HandleHangar;
            _results.NextClicked += HandleNext;
        }

        private void OnDisable()
        {
            _input.Controls.Tank.Pause.performed -= _onPauseKey;
            _input.Controls.UI.Cancel.performed -= _onCancel;
            _flow.Stack.Changed -= HandleStackChanged;
            if (_resultSource != null)
                _resultSource.Finished -= _onFinished;

            _pause.ResumeClicked -= HandleResume;
            _pause.SettingsClicked -= HandleSettings;
            _pause.RestartClicked -= HandleRestart;
            _pause.MenuClicked -= HandleMenu;
            _pause.QuitClicked -= HandleQuit;
            _results.AgainClicked -= HandleAgain;
            _results.HangarClicked -= HandleHangar;
            _results.NextClicked -= HandleNext;
        }

        private void Start()
        {
            DisableOldSandboxHud();
            _flow.Push(_hud);
            ResumeGame();
        }

        // TEMPORARY until the render team's VoxelSandbox scene (without VoxelDemoHud) is merged; then this goes
        // together with VoxelDemoHud.cs. Found by type name: the Game assembly does not reference the voxel demo.
        private static void DisableOldSandboxHud()
        {
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (behaviour.GetType().Name == OLD_SANDBOX_HUD)
                    behaviour.enabled = false;
        }

        // Leaving the level: the game-wide input and the player's pause must not stay switched off.
        private void OnDestroy()
        {
            if (_gameStopped)
                AppServices.Pause.Resume();
            _input.SetGameplayEnabled(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _flow.Dispose();
        }

        // ---------- Input ----------

        // A scene is loading: the battle is on its way out, nothing may push, pop or start another load.
        private static bool Busy => AppServices.GameFlow.IsBusy;

        private void HandlePauseKey(InputAction.CallbackContext _)
        {
            // The Esc that closed the pause (UI.Cancel, gameplay maps back on) must not reopen it.
            if (Busy || _finished || !ReferenceEquals(_flow.Stack.Top, _hud) || Time.frameCount == _pauseClosedFrame)
                return;

            _pauseOpenedFrame = Time.frameCount;
            StopGame();
            _flow.Push(_pause);
        }

        private void HandleCancel(InputAction.CallbackContext _)
        {
            if (Busy || ReferenceEquals(_flow.Stack.Top, _hud) || Time.frameCount == _pauseOpenedFrame)
                return;

            _flow.Back();
        }

        private void HandleStackChanged()
        {
            if (!_finished && _flow.Stack.Count == 1 && ReferenceEquals(_flow.Stack.Top, _hud) && _gameStopped)
            {
                _pauseClosedFrame = Time.frameCount;
                _hud.RefreshHints(); // the keys may have been rebound in the settings
                ResumeGame();
            }
        }

        private void StopGame()
        {
            _gameStopped = true;
            AppServices.Pause.Pause();
            _input.SetGameplayEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ResumeGame()
        {
            if (_gameStopped)
                AppServices.Pause.Resume();
            _gameStopped = false;
            _input.SetGameplayEnabled(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ---------- Pause ----------

        private void HandleResume()
        {
            if (!Busy)
                _flow.Back();
        }

        private void HandleSettings()
        {
            if (!Busy)
                _flow.Push(_settings);
        }

        private void HandleRestart()
        {
            if (Busy)
                return;

            _flow.Ask(UiStrings.Get("pause.restart_title"), UiStrings.Get("pause.progress_lost"),
                UiStrings.Get("pause.restart_yes"), UiStrings.Get("common.cancel"), yes =>
            {
                if (yes)
                    AppServices.GameFlow.RestartLevel();
            });
        }

        private void HandleMenu()
        {
            if (Busy)
                return;

            _flow.Ask(UiStrings.Get("pause.menu_title"), UiStrings.Get("pause.progress_lost"),
                UiStrings.Get("pause.menu_yes"), UiStrings.Get("common.cancel"), yes =>
            {
                if (yes)
                    AppServices.GameFlow.ReturnToMenu(HANGAR_SCREEN);
            });
        }

        private void HandleQuit()
        {
            if (Busy)
                return;

            _flow.Ask(UiStrings.Get("pause.quit_title"), UiStrings.Get("pause.progress_lost"),
                UiStrings.Get("pause.quit_yes"), UiStrings.Get("common.cancel"), yes =>
            {
                if (yes)
                    AppServices.GameFlow.Quit();
            });
        }

        // ---------- Results ----------

        private void HandleFinished(BattleResult result)
        {
            if (_finished || Busy)
                return;

            _finished = true;
            IPlayerProgress progress = AppServices.Progress;
            string levelId = AppServices.GameFlow.CurrentLevelId;
            LevelConfig level = _levelCatalog != null && !string.IsNullOrEmpty(levelId) ? _levelCatalog.Find(levelId) : null;

            // The reward paid is the one the results screen shows: the battle source's.
            if (result.Victory)
            {
                if (level != null)
                    ShopUseCase.CompleteLevel(progress, _levelCatalog, level, result.Reward);
                else if (result.Reward > 0)
                    progress.AddCredits(result.Reward); // the scene started straight, without a level
            }

            _nextLevel = LevelUnlockUseCase.Next(_levelCatalog, level);
            bool hasNext = _nextLevel != null && LevelUnlockUseCase.IsUnlocked(_nextLevel, progress);
            int killTarget = _battleStats is IBattleStats stats ? stats.KillTarget : 0;
            _results.Setup(result, level, killTarget, hasNext);

            StopGame();
            while (_flow.Stack.Count > 1)
                _flow.Stack.Pop();
            _flow.Stack.Replace(_results);
        }

        private void HandleAgain()
        {
            if (!Busy)
                AppServices.GameFlow.RestartLevel();
        }

        private void HandleHangar()
        {
            if (!Busy)
                AppServices.GameFlow.ReturnToMenu(HANGAR_SCREEN);
        }

        private void HandleNext()
        {
            if (_nextLevel != null && !Busy)
                AppServices.GameFlow.StartLevel(_nextLevel.Id);
        }
    }
}
