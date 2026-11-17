using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// «Настройки»: tabs «Графика», «Звук», «Управление» over a copy of the settings in effect. «Применить» saves
    /// the copy through ISettingsStore; leaving with unapplied changes asks «Применить изменения?». Volumes and
    /// keys change live while editing (preview), so a revert puts the saved ones back.
    /// </summary>
    public sealed class SettingsScreen : UIScreen, IBackHandler
    {
        [Header("Tabs")]
        [Tooltip("Графика, Звук, Управление — same order as the panels")]
        [SerializeField] private Button[] _tabButtons;
        [SerializeField] private TMP_Text[] _tabLabels;
        [SerializeField] private GameObject[] _tabPanels;
        [SerializeField] private GraphicsSettingsTab _graphics;
        [SerializeField] private AudioSettingsTab _audio;
        [SerializeField] private ControlsSettingsTab _controls;

        [Header("Buttons")]
        [SerializeField] private Button _apply;
        [SerializeField] private Button _defaults;
        [SerializeField] private Button _back;

        private ScreenFlow _flow;
        private GameSettings _edited;
        private string _savedOverrides = "";
        private bool _dirty;
        private bool _editing;
        private bool _subscribed;
        private UnityAction[] _tabHandlers;

        public void Bind(ScreenFlow flow)
        {
            _flow = flow;
            Subscribe();
        }

        public bool HandleBack()
        {
            if (_controls.IsRebinding || _controls.RebindEndedThisFrame)
            {
                _controls.CancelRebind();
                return true;
            }

            if (!_dirty)
                return false;

            _flow.Ask("Применить изменения?", "Изменения настроек ещё не применены.", "Применить", "Отменить",
                HandleLeaveAnswer);
            return true;
        }

        private void OnEnable()
        {
            Subscribe();
            // The stack opens a screen while it is still switched off, so Open may come before any subscription.
            if (!_editing)
                BeginEdit();

            _tabHandlers ??= CreateTabHandlers();
            for (int i = 0; i < _tabButtons.Length; i++)
                _tabButtons[i].onClick.AddListener(_tabHandlers[i]);
            _apply.onClick.AddListener(Apply);
            _defaults.onClick.AddListener(AskDefaults);
            _back.onClick.AddListener(HandleBackButton);
        }

        private void OnDisable()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
                _tabButtons[i].onClick.RemoveListener(_tabHandlers[i]);
            _apply.onClick.RemoveListener(Apply);
            _defaults.onClick.RemoveListener(AskDefaults);
            _back.onClick.RemoveListener(HandleBackButton);
        }

        private void OnDestroy()
        {
            if (!_subscribed)
                return;

            OnOpened -= BeginEdit;
            OnClosed -= HandleClosed;
            _graphics.Changed -= MarkDirty;
            _audio.Changed -= MarkDirty;
            _controls.Changed -= MarkDirty;
        }

        // From Bind or the first OnEnable, whichever comes first.
        private void Subscribe()
        {
            if (_subscribed)
                return;

            _subscribed = true;
            OnOpened += BeginEdit;
            OnClosed += HandleClosed;
            _graphics.Changed += MarkDirty;
            _audio.Changed += MarkDirty;
            _controls.Changed += MarkDirty;
        }

        private UnityAction[] CreateTabHandlers()
        {
            UnityAction[] handlers = new UnityAction[_tabButtons.Length];
            for (int i = 0; i < handlers.Length; i++)
            {
                int index = i;
                handlers[i] = () => ShowTab(index);
            }
            return handlers;
        }

        private void BeginEdit()
        {
            _edited = AppServices.SettingsStore.Current.Clone();
            _savedOverrides = AppServices.Input.SaveOverrides();
            _editing = true;
            BindTabs();
            SetDirty(false);
            ShowTab(0);
        }

        private void BindTabs()
        {
            _graphics.Bind(_edited.graphics);
            _audio.Bind(_edited.audio);
            _controls.Bind(_edited.controls);
        }

        private void ShowTab(int index)
        {
            _controls.CancelRebind();
            for (int i = 0; i < _tabPanels.Length; i++)
            {
                bool active = i == index;
                _tabPanels[i].SetActive(active);
                if (i < _tabLabels.Length && _tabLabels[i] != null)
                    _tabLabels[i].color = active ? UiStyle.Accent : UiStyle.TextMuted;
            }
        }

        private void Apply()
        {
            if (_edited == null)
                return;

            _controls.CancelRebind();
            _edited.controls.bindingOverrides = AppServices.Input.SaveOverrides();
            AppServices.SettingsStore.Save(_edited);

            // The saved object is the store's Current now: keep editing a fresh copy of it.
            _edited = AppServices.SettingsStore.Current.Clone();
            _savedOverrides = _edited.controls.bindingOverrides;
            BindTabs();
            SetDirty(false);
        }

        /// <summary>Puts the saved volumes and keys back (they were previewed live) and drops the edits.</summary>
        private void Revert()
        {
            _controls.CancelRebind();
            AudioSettingsUseCase.ApplyAll(AppServices.SettingsStore.Current.audio, AppServices.Audio);
            AppServices.Input.LoadOverrides(_savedOverrides);
            _edited = AppServices.SettingsStore.Current.Clone();
            SetDirty(false);
        }

        private void AskDefaults()
        {
            _controls.CancelRebind();
            _flow.Ask("Сбросить настройки?", "Графика, звук и клавиши вернутся к исходным. Потом нажмите «Применить».",
                "Сбросить", "Отмена", HandleDefaultsAnswer);
        }

        private void HandleDefaultsAnswer(bool yes)
        {
            if (!yes)
                return;

            _edited = new GameSettings();
            AppServices.Input.LoadOverrides("");
            AudioSettingsUseCase.ApplyAll(_edited.audio, AppServices.Audio);
            BindTabs();
            SetDirty(true);
        }

        // The answer comes after the dialog has left the stack: this screen is on top again and may pop itself.
        private void HandleLeaveAnswer(bool apply)
        {
            if (apply)
                Apply();
            else
                Revert();

            if (_flow.Stack.Top == (IScreen)this && _flow.Stack.Pop())
                AppServices.UiSounds.Play(UiSound.Back);
        }

        private void HandleBackButton() => _flow.Back();

        // Left some other way (the stack cleared, the scene changed) with edits pending: do not keep the preview.
        private void HandleClosed()
        {
            if (_dirty)
                Revert();
            _editing = false;
        }

        private void MarkDirty() => SetDirty(true);

        private void SetDirty(bool dirty)
        {
            _dirty = dirty;
            _apply.interactable = dirty;
        }
    }
}
