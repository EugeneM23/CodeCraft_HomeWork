using System;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The root of the menu over the 3D hangar: Continue (only with a save), New game, Settings, Quit.
    /// MenuFlow decides what each does.
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        public event Action ContinueClicked;
        public event Action NewGameClicked;
        public event Action SettingsClicked;
        public event Action QuitClicked;

        [SerializeField] private Button _continue;
        [SerializeField] private Button _newGame;
        [SerializeField] private Button _settings;
        [SerializeField] private Button _quit;
        [Tooltip("Shows the build version (Application.version)")]
        [SerializeField] private TMP_Text _version;

        private void Awake() => _version.text = string.Format(UiStrings.Get("main.version_format"), Application.version);

        /// <summary>Continue is shown only when there is a game to continue; the first button follows it.</summary>
        public void ShowContinue(bool show)
        {
            _continue.gameObject.SetActive(show);
            FirstSelected = show ? _continue : _newGame;
        }

        private void OnEnable()
        {
            _continue.onClick.AddListener(HandleContinue);
            _newGame.onClick.AddListener(HandleNewGame);
            _settings.onClick.AddListener(HandleSettings);
            _quit.onClick.AddListener(HandleQuit);
        }

        private void OnDisable()
        {
            _continue.onClick.RemoveListener(HandleContinue);
            _newGame.onClick.RemoveListener(HandleNewGame);
            _settings.onClick.RemoveListener(HandleSettings);
            _quit.onClick.RemoveListener(HandleQuit);
        }

        private void HandleContinue() => ContinueClicked?.Invoke();
        private void HandleNewGame() => NewGameClicked?.Invoke();
        private void HandleSettings() => SettingsClicked?.Invoke();
        private void HandleQuit() => QuitClicked?.Invoke();
    }
}
