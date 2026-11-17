using System;
using ScreenNavigation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// Owner of the main menu's screen stack (on the MenuUi root): opens the screens on their buttons, answers
    /// Esc with Back, asks Yes/No before buying and quitting, and hands level starts and quitting to IGameFlow.
    /// Screens only raise events; every move between them is decided here.
    /// </summary>
    public sealed class MenuFlow : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] private MainMenuScreen _mainMenu;
        [SerializeField] private LevelSelectScreen _levelSelect;
        [SerializeField] private HangarScreen _hangar;
        [SerializeField] private ShopScreen _shop;
        [SerializeField] private SettingsScreen _settings;
        [SerializeField] private ConfirmDialog _dialog;

        [Header("Data")]
        [SerializeField] private LevelCatalogConfig _levelCatalog;
        [SerializeField] private ShopCatalogConfig _shopCatalog;

        private ScreenFlow _flow;
        private IGameFlow _gameFlow;
        private IPlayerProgress _progress;
        private InputAction _cancel;
        private Action<bool> _quitAnswer;
        private Action<bool> _buyAnswer;
        private TankModuleConfig _pendingBuy;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _gameFlow = AppServices.GameFlow;
            _progress = AppServices.Progress;
            AppServices.RegisterLevels(_levelCatalog);
            ShopUseCase.EnsureStarted(_progress, _shopCatalog, _levelCatalog);

            _levelSelect.Init(_levelCatalog);
            _hangar.Init(_shopCatalog);
            _shop.Init(_shopCatalog);

            _flow = new ScreenFlow(_dialog);
            _settings.Bind(_flow);
            _quitAnswer = HandleQuitAnswer;
            _buyAnswer = HandleBuyAnswer;

            _mainMenu.PlayClicked += HandlePlay;
            _mainMenu.HangarClicked += HandleHangar;
            _mainMenu.ShopClicked += HandleShop;
            _mainMenu.SettingsClicked += HandleSettings;
            _mainMenu.QuitClicked += HandleQuit;
            _levelSelect.StartClicked += HandleStart;
            _levelSelect.HangarClicked += HandleHangar;
            _levelSelect.BackClicked += HandleBack;
            _hangar.ShopClicked += HandleHangarShop;
            _hangar.BackClicked += HandleBack;
            _shop.BuyClicked += HandleBuy;
            _shop.BackClicked += HandleBack;

            _cancel = AppServices.Input.Controls.UI.Cancel;
            _cancel.performed += HandleCancel;

            _flow.Push(_mainMenu);
            string requested = _gameFlow.RequestedMenuScreen;
            if (requested == MenuScreens.HANGAR)
                _flow.Push(_hangar);
            else if (requested == MenuScreens.LEVEL_SELECT)
                _flow.Push(_levelSelect);
        }

        private void OnDestroy()
        {
            if (_flow == null)
                return;

            _mainMenu.PlayClicked -= HandlePlay;
            _mainMenu.HangarClicked -= HandleHangar;
            _mainMenu.ShopClicked -= HandleShop;
            _mainMenu.SettingsClicked -= HandleSettings;
            _mainMenu.QuitClicked -= HandleQuit;
            _levelSelect.StartClicked -= HandleStart;
            _levelSelect.HangarClicked -= HandleHangar;
            _levelSelect.BackClicked -= HandleBack;
            _hangar.ShopClicked -= HandleHangarShop;
            _hangar.BackClicked -= HandleBack;
            _shop.BuyClicked -= HandleBuy;
            _shop.BackClicked -= HandleBack;
            _cancel.performed -= HandleCancel;
            _flow.Dispose();
        }

        private bool Busy => _gameFlow.IsBusy;

        /// <summary>
        /// Pushes the screen, or, when it is already in the stack (hangar → shop after shop → … ), pops back to it:
        /// ScreenStack does not take one screen twice.
        /// </summary>
        private void Open(UIScreen screen)
        {
            if (Busy)
                return;

            ScreenStack stack = _flow.Stack;
            if (!stack.Contains(screen))
            {
                _flow.Push(screen);
                return;
            }

            while (stack.Top != screen)
                stack.Pop();
        }

        private void HandleCancel(InputAction.CallbackContext context)
        {
            if (!Busy)
                _flow.Back();
        }

        private void HandleBack()
        {
            if (!Busy)
                _flow.Back();
        }

        private void HandlePlay() => Open(_levelSelect);
        private void HandleHangar() => Open(_hangar);
        private void HandleSettings() => Open(_settings);

        private void HandleShop() => Open(_shop);

        private void HandleHangarShop(TankSlot slot)
        {
            _shop.ShowSlot(slot);
            Open(_shop);
        }

        private void HandleStart(LevelConfig level)
        {
            if (Busy || level == null || !LevelCatalogConfig.IsUnlocked(level, _progress))
                return;

            _gameFlow.StartLevel(level.id);
        }

        private void HandleQuit()
        {
            if (!Busy)
                _flow.Ask("Выход", "Выйти из игры?", "Выйти", "Отмена", _quitAnswer);
        }

        private void HandleQuitAnswer(bool yes)
        {
            if (yes)
                _gameFlow.Quit();
        }

        private void HandleBuy(TankModuleConfig module)
        {
            if (Busy || module == null)
                return;

            _pendingBuy = module;
            int credits = _progress.Credits;
            string text = "Купить «" + module.displayName + "» за " + module.price + "?\nОстанется " +
                          (credits - module.price) + " кредитов.";
            _flow.Ask("Покупка", text, "Купить", "Отмена", _buyAnswer);
        }

        private void HandleBuyAnswer(bool yes)
        {
            TankModuleConfig module = _pendingBuy;
            _pendingBuy = null;
            if (!yes || module == null)
                return;

            bool bought = ShopUseCase.TryBuy(_progress, module);
            AppServices.UiSounds.Play(bought ? UiSound.Purchase : UiSound.Error);
        }
    }
}
