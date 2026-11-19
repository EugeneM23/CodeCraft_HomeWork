using System;
using ScreenNavigation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// Owner of the main menu's screen stack (on the MenuUi root): opens the screens on their buttons, answers
    /// Esc with Back, asks Yes/No before a new game, a purchase and quitting, hands chapter starts to IHangarSource
    /// and quitting to IGameFlow, and moves the 3D hangar's camera: the hangar framing while the hangar, the slot
    /// assembly or the upgrade is on top, the menu framing otherwise. Screens only raise events; every move between
    /// them is decided here.
    /// </summary>
    public sealed class MenuFlow : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] private MainMenuScreen _mainMenu;
        [SerializeField] private HangarScreen _hangar;
        [SerializeField] private SlotAssemblyScreen _slotAssembly;
        [SerializeField] private UpgradeScreen _upgrade;
        [SerializeField] private ShopScreen _shop;
        [SerializeField] private ChapterSelectScreen _chapterSelect;
        [SerializeField] private SettingsScreen _settings;
        [SerializeField] private ConfirmDialog _dialog;

        [Header("Scene")]
        [Tooltip("The 3D hangar behind the menu (scene object, set by MenuUiBuilder.BuildScene); may be empty")]
        [SerializeField] private HangarStage _stage;
        [Tooltip("The core's level catalog: the stub game flow maps level ids to scenes with it")]
        [SerializeField] private LevelCatalog _levelCatalog;

        private ScreenFlow _flow;
        private IGameFlow _gameFlow;
        private IHangarSource _source;
        private InputAction _cancel;
        private Action<bool> _quitAnswer;
        private Action<bool> _newGameAnswer;
        private Action<bool> _buyAnswer;
        private string _pendingBuy;
        private int _stageView = -1;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _gameFlow = AppServices.GameFlow;
            _source = AppServices.Hangar;
            AppServices.RegisterLevels(_levelCatalog);

            _flow = new ScreenFlow(_dialog);
            _settings.Bind(_flow);
            _quitAnswer = HandleQuitAnswer;
            _newGameAnswer = HandleNewGameAnswer;
            _buyAnswer = HandleBuyAnswer;

            _mainMenu.ContinueClicked += HandleContinue;
            _mainMenu.NewGameClicked += HandleNewGame;
            _mainMenu.SettingsClicked += HandleSettings;
            _mainMenu.QuitClicked += HandleQuit;
            _hangar.SlotClicked += HandleSlot;
            _hangar.ShopClicked += HandleShop;
            _hangar.UpgradeClicked += HandleUpgrade;
            _hangar.BattleClicked += HandleBattle;
            _hangar.BackClicked += HandleBack;
            _slotAssembly.CloseClicked += HandleClose;
            _upgrade.CloseClicked += HandleClose;
            _shop.BuyClicked += HandleBuy;
            _shop.BackClicked += HandleBack;
            _chapterSelect.StartClicked += HandleStart;
            _chapterSelect.BackClicked += HandleBack;
            _flow.Stack.Changed += UpdateStage;

            _cancel = AppServices.Input.Controls.UI.Cancel;
            _cancel.performed += HandleCancel;

            _mainMenu.ShowContinue(_source.HasSave);
            _flow.Push(_mainMenu);
            string requested = _gameFlow.RequestedMenuScreen;
            if (requested == MenuScreens.HANGAR)
                _flow.Push(_hangar);
            else if (requested == MenuScreens.CHAPTER_SELECT)
                _flow.Push(_chapterSelect);
            UpdateStage();
        }

        private void OnDestroy()
        {
            if (_flow == null)
                return;

            _mainMenu.ContinueClicked -= HandleContinue;
            _mainMenu.NewGameClicked -= HandleNewGame;
            _mainMenu.SettingsClicked -= HandleSettings;
            _mainMenu.QuitClicked -= HandleQuit;
            _hangar.SlotClicked -= HandleSlot;
            _hangar.ShopClicked -= HandleShop;
            _hangar.UpgradeClicked -= HandleUpgrade;
            _hangar.BattleClicked -= HandleBattle;
            _hangar.BackClicked -= HandleBack;
            _slotAssembly.CloseClicked -= HandleClose;
            _upgrade.CloseClicked -= HandleClose;
            _shop.BuyClicked -= HandleBuy;
            _shop.BackClicked -= HandleBack;
            _chapterSelect.StartClicked -= HandleStart;
            _chapterSelect.BackClicked -= HandleBack;
            _flow.Stack.Changed -= UpdateStage;
            _cancel.performed -= HandleCancel;
            _flow.Dispose();
        }

        private bool Busy => _gameFlow.IsBusy;

        /// <summary>
        /// Pushes the screen, or, when it is already in the stack (hangar → shop → … ), pops back to it:
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

            while (!ReferenceEquals(stack.Top, screen))
                stack.Pop();
        }

        // The hangar framing for the hangar and its overlays (a dialog over them keeps it), the menu framing otherwise
        private void UpdateStage()
        {
            if (_stage == null || _flow == null)
                return;

            // The Yes/No dialog sits over another screen: it keeps the framing that screen chose
            IScreen top = _flow.Stack.Top;
            if (ReferenceEquals(top, _dialog))
                return;

            int view = IsHangarView(top) ? 1 : 0;
            if (view == _stageView)
                return;

            _stageView = view;
            if (view == 1)
                _stage.ShowHangar();
            else
                _stage.ShowMenu();
        }

        private bool IsHangarView(IScreen screen) =>
            ReferenceEquals(screen, _hangar) || ReferenceEquals(screen, _slotAssembly) || ReferenceEquals(screen, _upgrade);

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

        // An overlay's own close button: plays Close (the Esc path plays ScreenFlow's Back)
        private void HandleClose()
        {
            if (Busy)
                return;

            if (_flow.Stack.Pop())
                AppServices.UiSounds.Play(UiSound.Close);
        }

        // ---------- Main menu ----------

        private void HandleContinue()
        {
            if (_source.HasSave)
                Open(_hangar);
        }

        private void HandleNewGame()
        {
            if (Busy)
                return;

            if (!_source.HasSave)
            {
                StartNewGame();
                return;
            }

            _flow.Ask(UiStrings.Get("hangar.new_game_title"), UiStrings.Get("hangar.new_game_text"),
                UiStrings.Get("hangar.new_game_yes"), UiStrings.Get("common.cancel"), _newGameAnswer);
        }

        private void HandleNewGameAnswer(bool yes)
        {
            if (yes)
                StartNewGame();
            else
                AppServices.UiSounds.Play(UiSound.Close);
        }

        private void StartNewGame()
        {
            _source.NewGame();
            _mainMenu.ShowContinue(_source.HasSave);
            Open(_hangar);
        }

        private void HandleSettings() => Open(_settings);

        private void HandleQuit()
        {
            if (!Busy)
                _flow.Ask(UiStrings.Get("main.quit_title"), UiStrings.Get("main.quit_text"), UiStrings.Get("main.quit_yes"),
                    UiStrings.Get("common.cancel"), _quitAnswer);
        }

        private void HandleQuitAnswer(bool yes)
        {
            if (yes)
                _gameFlow.Quit();
            else
                AppServices.UiSounds.Play(UiSound.Close);
        }

        // ---------- Hangar ----------

        private void HandleSlot(string slotId)
        {
            _slotAssembly.ShowSlot(slotId);
            Open(_slotAssembly);
        }

        private void HandleShop() => Open(_shop);
        private void HandleUpgrade() => Open(_upgrade);
        private void HandleBattle() => Open(_chapterSelect);

        private void HandleStart(string chapterId)
        {
            if (!Busy && chapterId != null)
                _source.StartChapter(chapterId);
        }

        // ---------- Shop ----------

        private void HandleBuy(string moduleId)
        {
            if (Busy || moduleId == null)
                return;

            HangarModule module = _source.Module(moduleId);
            if (!module.IsValid)
                return;

            _pendingBuy = moduleId;
            string moduleName = HangarLook.ModuleName(moduleId);
            int marksLeft = _source.Marks - module.PriceMarks;
            string text;
            if (module.PriceSamples > 0)
            {
                // A price in samples too: what remains of both.
                string price = string.Format(UiStrings.Get("hangar.price_both_format"), module.PriceMarks, module.PriceSamples);
                text = string.Format(UiStrings.Get("hangar.buy_text_both_format"), moduleName, price, marksLeft,
                    _source.Samples - module.PriceSamples);
            }
            else
            {
                string price = string.Format(UiStrings.Get("hangar.price_marks_format"), module.PriceMarks);
                text = string.Format(UiStrings.Get("hangar.buy_text_format"), moduleName, price, marksLeft);
            }
            _flow.Ask(UiStrings.Get("hangar.buy_title"), text, UiStrings.Get("hangar.shop.buy"),
                UiStrings.Get("common.cancel"), _buyAnswer);
        }

        // The one purchase sound: the buy buttons click plainly and only open the question.
        private void HandleBuyAnswer(bool yes)
        {
            string moduleId = _pendingBuy;
            _pendingBuy = null;
            if (!yes || moduleId == null)
            {
                AppServices.UiSounds.Play(UiSound.Close);
                return;
            }

            PurchaseResult result = _source.TryBuy(moduleId);
            AppServices.UiSounds.Play(result == PurchaseResult.Done ? UiSound.Purchase : UiSound.Error);
        }
    }
}
