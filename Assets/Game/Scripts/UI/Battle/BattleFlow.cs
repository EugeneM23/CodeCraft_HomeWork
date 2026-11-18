using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The battle UI's orchestrator, on the BattleUi root. One screen stack: the HUD at the root; over it the
    /// pause (settings and Yes/No questions above that) or the skill choice of a new level; the run results in
    /// place of the HUD at the end. The run is read through IRunHudSource (AppServices.RunHud).
    /// While anything covers the HUD the game is paused, the gameplay maps (Tank, Sandbox) are off and the
    /// cursor is free; back on the bare HUD the game runs, the tank drives and the cursor is locked.
    /// A level up that arrives while the pause is open waits until the player is back on the HUD.
    /// Esc is both Tank.Pause and UI.Cancel: Pause opens the pause only from the HUD and not in the frame the
    /// pause closed, Cancel goes back only when the HUD is not on top and not in the frame the pause opened,
    /// so one press does one thing. The skill choice ignores Esc. While the game flow loads a scene, keys and
    /// buttons do nothing.
    /// </summary>
    public sealed class BattleFlow : MonoBehaviour
    {
        private const string HANGAR_SCREEN = "Hangar";

        [SerializeField] private HudScreen _hud;
        [SerializeField] private PauseScreen _pause;
        [SerializeField] private SkillChoiceScreen _skillChoice;
        [SerializeField] private RunResultsScreen _results;
        [SerializeField] private ConfirmDialog _confirmDialog;
        [SerializeField] private SettingsScreen _settings;
        [Tooltip("Lets the stand-in game flow restart the level when the scene was started on its own")]
        [SerializeField] private LevelCatalog _levelCatalog;

        private ScreenFlow _flow;
        private IGameInput _input;
        private IRunHudSource _run;
        private IReadOnlyList<SkillOffer> _waitingOffers;
        private int _pauseOpenedFrame = -1;
        private int _pauseClosedFrame = -1;
        private bool _gameStopped;
        private bool _pauseShown;
        private bool _finished;

        private Action<InputAction.CallbackContext> _onPauseKey;
        private Action<InputAction.CallbackContext> _onCancel;
        private Action<IReadOnlyList<SkillOffer>> _onSkillOffered;
        private Action<RunSummary> _onRunEnded;
        private Action<int> _onSkillChosen;

        private void Awake()
        {
            AppServices.RegisterLevels(_levelCatalog);
            // The core's stand-in Esc pause gives Esc over to this pause screen while the battle UI lives.
            PauseHotkey.Enabled = false;
            _input = AppServices.Input;
            _run = AppServices.RunHud;
            _flow = new ScreenFlow(_confirmDialog);
            _settings.Bind(_flow);
            _hud.SetRun(_run);

            _onPauseKey = HandlePauseKey;
            _onCancel = HandleCancel;
            _onSkillOffered = HandleSkillOffered;
            _onRunEnded = HandleRunEnded;
            _onSkillChosen = HandleSkillChosen;
        }

        private void OnEnable()
        {
            _input.Controls.Tank.Pause.performed += _onPauseKey;
            _input.Controls.UI.Cancel.performed += _onCancel;
            _flow.Stack.Changed += HandleStackChanged;
            _run.SkillOffered += _onSkillOffered;
            _run.RunEnded += _onRunEnded;

            _pause.ResumeClicked += HandleResume;
            _pause.SettingsClicked += HandleSettings;
            _pause.ExitClicked += HandleExit;
            _skillChoice.Chosen += _onSkillChosen;
            _results.HangarClicked += HandleHangar;
            _results.AgainClicked += HandleAgain;
        }

        private void OnDisable()
        {
            _input.Controls.Tank.Pause.performed -= _onPauseKey;
            _input.Controls.UI.Cancel.performed -= _onCancel;
            _flow.Stack.Changed -= HandleStackChanged;
            _run.SkillOffered -= _onSkillOffered;
            _run.RunEnded -= _onRunEnded;

            _pause.ResumeClicked -= HandleResume;
            _pause.SettingsClicked -= HandleSettings;
            _pause.ExitClicked -= HandleExit;
            _skillChoice.Chosen -= _onSkillChosen;
            _results.HangarClicked -= HandleHangar;
            _results.AgainClicked -= HandleAgain;
        }

        private void Start()
        {
            _flow.Push(_hud);
            ResumeGame();
        }

        // Leaving the level: the game-wide input and the player's pause must not stay switched off.
        private void OnDestroy()
        {
            PauseHotkey.Enabled = true;
            if (_gameStopped)
                AppServices.Pause.Resume();
            _input.SetGameplayEnabled(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _hud.SetRun(null);
            _flow.Dispose();
        }

        // ---------- Input ----------

        // A scene is loading: the battle is on its way out, nothing may push, pop or start another load.
        private static bool Busy => AppServices.GameFlow.IsBusy;

        private bool OnBareHud => _flow.Stack.Count == 1 && ReferenceEquals(_flow.Stack.Top, _hud);

        private void HandlePauseKey(InputAction.CallbackContext _)
        {
            // The Esc that closed the pause (UI.Cancel, gameplay maps back on) must not reopen it.
            if (Busy || _finished || !OnBareHud || Time.frameCount == _pauseClosedFrame)
                return;

            _pauseOpenedFrame = Time.frameCount;
            StopGame();
            _pauseShown = true;
            _pause.ShowSkills(_run.Skills);
            _flow.Stack.Push(_pause);
            AppServices.UiSounds.Play(UiSound.PauseOn);
        }

        private void HandleCancel(InputAction.CallbackContext _)
        {
            if (Busy || ReferenceEquals(_flow.Stack.Top, _hud) || Time.frameCount == _pauseOpenedFrame)
                return;

            _flow.Back();
        }

        private void HandleStackChanged()
        {
            if (_finished || !OnBareHud || !_gameStopped)
                return;

            if (_pauseShown)
            {
                _pauseShown = false;
                _pauseClosedFrame = Time.frameCount;
                AppServices.UiSounds.Play(UiSound.PauseOff);
                _hud.RefreshHints(); // the keys may have been rebound in the settings
            }

            // A level up came while the pause was open: its choice now, the game stays stopped.
            if (_waitingOffers != null)
            {
                IReadOnlyList<SkillOffer> offers = _waitingOffers;
                _waitingOffers = null;
                ShowSkillChoice(offers);
                return;
            }

            ResumeGame();
        }

        private void StopGame()
        {
            if (!_gameStopped)
                AppServices.Pause.Pause();
            _gameStopped = true;
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

        private void HandleExit()
        {
            if (Busy)
                return;

            _flow.Ask(UiStrings.Get("run.pause_exit_title"), UiStrings.Get("run.pause_exit_text"),
                UiStrings.Get("run.pause_exit_yes"), UiStrings.Get("common.cancel"), yes =>
            {
                if (yes && !Busy)
                    _run.ExitToHangar(); // RunEnded (Abandoned) takes the player to the hangar
            });
        }

        // ---------- Skill choice ----------

        private void HandleSkillOffered(IReadOnlyList<SkillOffer> offers)
        {
            if (_finished || Busy || offers == null || offers.Count == 0 || ReferenceEquals(_flow.Stack.Top, _skillChoice))
                return;

            if (!OnBareHud)
            {
                _waitingOffers = offers; // the pause is open: shown when the player is back on the HUD
                return;
            }

            ShowSkillChoice(offers);
        }

        private void ShowSkillChoice(IReadOnlyList<SkillOffer> offers)
        {
            StopGame();
            _skillChoice.Setup(offers);
            _flow.Stack.Push(_skillChoice);
            AppServices.UiSounds.Play(UiSound.LevelUp);
        }

        private void HandleSkillChosen(int index)
        {
            if (Busy || !ReferenceEquals(_flow.Stack.Top, _skillChoice))
                return;

            _run.ChooseSkill(index);
            AppServices.UiSounds.Play(UiSound.SkillTaken);
            _flow.Stack.Pop(); // back on the bare HUD: HandleStackChanged resumes the game
        }

        // ---------- Results ----------

        private void HandleRunEnded(RunSummary summary)
        {
            if (_finished || summary == null)
                return;

            _finished = true;
            _waitingOffers = null;

            // Left from the pause: straight to the hangar, what was collected is kept by the run.
            if (summary.Abandoned)
            {
                if (!Busy)
                    AppServices.GameFlow.ReturnToMenu(HANGAR_SCREEN);
                return;
            }

            _results.Setup(summary);
            StopGame();
            while (_flow.Stack.Count > 1)
                _flow.Stack.Pop();
            _flow.Stack.Replace(_results);
            AppServices.UiSounds.Play(summary.Victory ? UiSound.Victory : UiSound.Defeat);
        }

        private void HandleHangar()
        {
            if (!Busy)
                AppServices.GameFlow.ReturnToMenu(HANGAR_SCREEN);
        }

        // Until the core's run service: a new run is the level scene loaded again.
        private void HandleAgain()
        {
            if (!Busy)
                AppServices.GameFlow.RestartLevel();
        }
    }
}
