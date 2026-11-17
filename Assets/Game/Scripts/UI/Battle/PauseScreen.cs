using System;
using ScreenNavigation;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>The pause over the battle: Continue, Settings, Restart, To menu, Quit. BattleFlow decides what they do.</summary>
    public sealed class PauseScreen : UIScreen
    {
        public event Action ResumeClicked;
        public event Action SettingsClicked;
        public event Action RestartClicked;
        public event Action MenuClicked;
        public event Action QuitClicked;

        [SerializeField] private Button _resume;
        [SerializeField] private Button _settings;
        [SerializeField] private Button _restart;
        [SerializeField] private Button _menu;
        [SerializeField] private Button _quit;

        private void OnEnable()
        {
            _resume.onClick.AddListener(HandleResume);
            _settings.onClick.AddListener(HandleSettings);
            _restart.onClick.AddListener(HandleRestart);
            _menu.onClick.AddListener(HandleMenu);
            _quit.onClick.AddListener(HandleQuit);
        }

        private void OnDisable()
        {
            _resume.onClick.RemoveListener(HandleResume);
            _settings.onClick.RemoveListener(HandleSettings);
            _restart.onClick.RemoveListener(HandleRestart);
            _menu.onClick.RemoveListener(HandleMenu);
            _quit.onClick.RemoveListener(HandleQuit);
        }

        private void HandleResume() => ResumeClicked?.Invoke();
        private void HandleSettings() => SettingsClicked?.Invoke();
        private void HandleRestart() => RestartClicked?.Invoke();
        private void HandleMenu() => MenuClicked?.Invoke();
        private void HandleQuit() => QuitClicked?.Invoke();
    }
}
