using System;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>The root of the menu: Play, Hangar, Shop, Settings, Quit. MenuFlow decides what each does.</summary>
    public sealed class MainMenuScreen : UIScreen
    {
        public event Action PlayClicked;
        public event Action HangarClicked;
        public event Action ShopClicked;
        public event Action SettingsClicked;
        public event Action QuitClicked;

        [SerializeField] private Button _play;
        [SerializeField] private Button _hangar;
        [SerializeField] private Button _shop;
        [SerializeField] private Button _settings;
        [SerializeField] private Button _quit;
        [Tooltip("Shows the build version (Application.version)")]
        [SerializeField] private TMP_Text _version;

        private void Awake() => _version.text = "Версия " + Application.version;

        private void OnEnable()
        {
            _play.onClick.AddListener(HandlePlay);
            _hangar.onClick.AddListener(HandleHangar);
            _shop.onClick.AddListener(HandleShop);
            _settings.onClick.AddListener(HandleSettings);
            _quit.onClick.AddListener(HandleQuit);
        }

        private void OnDisable()
        {
            _play.onClick.RemoveListener(HandlePlay);
            _hangar.onClick.RemoveListener(HandleHangar);
            _shop.onClick.RemoveListener(HandleShop);
            _settings.onClick.RemoveListener(HandleSettings);
            _quit.onClick.RemoveListener(HandleQuit);
        }

        private void HandlePlay() => PlayClicked?.Invoke();
        private void HandleHangar() => HangarClicked?.Invoke();
        private void HandleShop() => ShopClicked?.Invoke();
        private void HandleSettings() => SettingsClicked?.Invoke();
        private void HandleQuit() => QuitClicked?.Invoke();
    }
}
