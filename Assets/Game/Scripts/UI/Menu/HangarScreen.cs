using System;
using System.Collections.Generic;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The hangar: tank slots on the left, the stats on the right (with the difference a hovered or selected module
    /// would make), the modules of the selected slot at the bottom with Install and Upgrade. Changes the progress
    /// through ShopUseCase; redraws on IPlayerProgress.Changed. Slot rows and module items are cloned from
    /// switched-off template children and reused.
    /// </summary>
    public sealed class HangarScreen : UIScreen
    {
        // Children of the slot row and module item templates, by name (Hangar part of MenuUiBuilder)
        public const string ITEM_MARKER = "Marker";
        public const string ITEM_NAME = "Name";
        public const string ITEM_VALUE = "Value";

        /// <summary>UiStrings keys of the slot names, by (int)TankSlot.</summary>
        public static readonly string[] SlotNameKeys =
            { "slot.gun", "slot.machine_gun", "slot.hull", "slot.engine", "slot.tracks", "slot.armor" };

        // IPlayerProgress keys of the slots (TankSlot.ToString()), by (int)TankSlot
        private static readonly string[] SlotKeys = Enum.GetNames(typeof(TankSlot));

        // Value formats of the stats, by TankStats index (TMP SetText: {0:N} = N decimals)
        private static readonly string[] ValueFormats = { "{0:0}", "{0:0}", "{0:1}", "{0:0}", "{0:1}", "{0:1}" };
        private static readonly string[] PlusFormats = { "+{0:0}", "+{0:0}", "+{0:1}", "+{0:0}", "+{0:1}", "+{0:1}" };

        public event Action<TankSlot> ShopClicked;
        public event Action BackClicked;

        [Header("Slots")]
        [Tooltip("Switched-off slot row (children Marker, Name, Value); cloned once per TankSlot")]
        [SerializeField] private Button _slotTemplate;

        [Header("Stats")]
        [Tooltip("Value texts in the order of TankStats (health, armor, speed, damage, reload, fire rate)")]
        [SerializeField] private TMP_Text[] _statValues = new TMP_Text[TankStats.COUNT];
        [Tooltip("Difference texts in the same order")]
        [SerializeField] private TMP_Text[] _statDeltas = new TMP_Text[TankStats.COUNT];

        [Header("Modules")]
        [SerializeField] private TMP_Text _modulesTitle;
        [Tooltip("Switched-off module item (children Marker, Name, Value); cloned as needed")]
        [SerializeField] private Button _moduleTemplate;
        [SerializeField] private TMP_Text _moduleName;
        [SerializeField] private TMP_Text _moduleLevel;
        [SerializeField] private TMP_Text _moduleDescription;
        [SerializeField] private Button _install;
        [SerializeField] private TMP_Text _installLabel;
        [SerializeField] private Button _upgrade;
        [SerializeField] private TMP_Text _upgradeLabel;
        [Tooltip("Shown for a module that is not bought: opens the shop on its slot")]
        [SerializeField] private Button _buyInShop;

        [Header("Navigation")]
        [SerializeField] private Button _shop;
        [SerializeField] private Button _back;

        private readonly List<Item> _slots = new();
        private readonly List<Item> _modules = new();
        private readonly List<TankModuleConfig> _slotModules = new();
        private readonly float[] _shownValues = new float[TankStats.COUNT];
        private readonly float[] _shownDeltas = new float[TankStats.COUNT];

        private ShopCatalogConfig _catalog;
        private IPlayerProgress _progress;
        private TankSlot _slot;
        private TankModuleConfig _selectedModule;
        private TankModuleConfig _previewModule;
        private bool _statsShown;

        // Texts from UiStrings, fetched once in Awake
        private readonly string[] _slotNames = new string[SlotNameKeys.Length];
        private string _slotModulesFormat;
        private string _slotValueFormat;
        private string _moduleInstalled;
        private string _moduleOwnedFormat;
        private string _moduleInShopFormat;
        private string _noModules;
        private string _levelFormat;
        private string _notOwned;
        private string _installText;
        private string _installedText;
        private string _upgradeFormat;

        /// <summary>Called by MenuFlow before the screen is first shown.</summary>
        public void Init(ShopCatalogConfig catalog)
        {
            _catalog = catalog;
            _progress = AppServices.Progress;
        }

        private void Awake()
        {
            for (int i = 0; i < SlotNameKeys.Length; i++)
                _slotNames[i] = UiStrings.Get(SlotNameKeys[i]);
            _slotModulesFormat = UiStrings.Get("hangar.slot_modules_format");
            _slotValueFormat = UiStrings.Get("hangar.slot_value_format");
            _moduleInstalled = UiStrings.Get("hangar.module_installed");
            _moduleOwnedFormat = UiStrings.Get("hangar.module_owned_format");
            _moduleInShopFormat = UiStrings.Get("hangar.module_in_shop_format");
            _noModules = UiStrings.Get("hangar.no_modules");
            _levelFormat = UiStrings.Get("hangar.level_format");
            _notOwned = UiStrings.Get("hangar.not_owned");
            _installText = UiStrings.Get("hangar.install");
            _installedText = UiStrings.Get("hangar.installed");
            _upgradeFormat = UiStrings.Get("hangar.upgrade_format");
        }

        private void OnEnable()
        {
            _install.onClick.AddListener(HandleInstall);
            _upgrade.onClick.AddListener(HandleUpgrade);
            _buyInShop.onClick.AddListener(HandleShop);
            _shop.onClick.AddListener(HandleShop);
            _back.onClick.AddListener(HandleBack);
            _progress.Changed += Refresh;

            EnsureSlots();
            SelectSlot(_slot);
        }

        private void OnDisable()
        {
            _install.onClick.RemoveListener(HandleInstall);
            _upgrade.onClick.RemoveListener(HandleUpgrade);
            _buyInShop.onClick.RemoveListener(HandleShop);
            _shop.onClick.RemoveListener(HandleShop);
            _back.onClick.RemoveListener(HandleBack);
            _progress.Changed -= Refresh;
        }

        // Hover selects (SelectOnPointerEnter): a selected module item previews its stats, else the clicked module does
        protected override void Update() // perf-ok: ticks during pause (menu)
        {
            base.Update();

            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            TankModuleConfig preview = _selectedModule;
            for (int i = 0; i < _slotModules.Count; i++)
            {
                if (_modules[i].Button.gameObject == selected)
                {
                    preview = _slotModules[i];
                    break;
                }
            }

            if (preview != _previewModule)
            {
                _previewModule = preview;
                RefreshStats();
            }
        }

        private void Refresh()
        {
            RefreshSlots();
            RefreshModules();
            RefreshDetails();
            RefreshStats();
        }

        private void SelectSlot(TankSlot slot)
        {
            _slot = slot;
            _selectedModule = _catalog.Find(_progress.GetInstalledModule(SlotKeys[(int)slot]));
            _modulesTitle.text = string.Format(_slotModulesFormat, _slotNames[(int)slot].ToUpperInvariant());
            CollectSlotModules();
            if (_selectedModule == null && _slotModules.Count > 0)
                _selectedModule = _slotModules[0];
            _previewModule = _selectedModule;
            Refresh();
        }

        private void SelectModule(int index)
        {
            if (index < 0 || index >= _slotModules.Count)
                return;

            _selectedModule = _slotModules[index];
            _previewModule = _selectedModule;
            RefreshModules();
            RefreshDetails();
            RefreshStats();
        }

        // ---------- Slots ----------

        private void EnsureSlots()
        {
            if (_slots.Count > 0)
                return;

            for (int i = 0; i < _slotNames.Length; i++)
            {
                Item item = Clone(_slotTemplate, "Slot_" + SlotKeys[i]);
                TankSlot slot = (TankSlot)i;
                item.Button.onClick.AddListener(() => SelectSlot(slot)); // once per row, at creation
                item.Name.text = _slotNames[i];
                _slots.Add(item);
            }
        }

        private void RefreshSlots()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                Item item = _slots[i];
                item.Marker.SetActive(i == (int)_slot);
                TankModuleConfig module = _catalog.Find(_progress.GetInstalledModule(SlotKeys[i]));
                item.Value.text = module != null
                    ? string.Format(_slotValueFormat, module.displayName, _progress.GetModuleLevel(module.id))
                    : "—";
            }
        }

        // ---------- Modules ----------

        private void CollectSlotModules()
        {
            _slotModules.Clear();
            for (int i = 0; i < _catalog.modules.Count; i++)
            {
                TankModuleConfig module = _catalog.modules[i];
                if (module != null && module.slot == _slot)
                    _slotModules.Add(module);
            }
        }

        private void RefreshModules()
        {
            while (_modules.Count < _slotModules.Count)
            {
                Item item = Clone(_moduleTemplate, "Module_" + _modules.Count);
                int index = _modules.Count;
                item.Button.onClick.AddListener(() => SelectModule(index)); // once per item, at creation
                _modules.Add(item);
            }

            for (int i = 0; i < _modules.Count; i++)
            {
                Item item = _modules[i];
                bool used = i < _slotModules.Count;
                item.Button.gameObject.SetActive(used);
                if (!used)
                    continue;

                TankModuleConfig module = _slotModules[i];
                item.Name.text = module.displayName;
                item.Marker.SetActive(module == _selectedModule);
                if (ShopUseCase.IsInstalled(_progress, module))
                {
                    item.Value.text = _moduleInstalled;
                    item.Value.color = UiStyle.Accent;
                }
                else if (ShopUseCase.IsOwned(_progress, module))
                {
                    item.Value.SetText(_moduleOwnedFormat, _progress.GetModuleLevel(module.id));
                    item.Value.color = UiStyle.Good;
                }
                else
                {
                    item.Value.SetText(_moduleInShopFormat, module.price);
                    item.Value.color = UiStyle.TextMuted;
                }
            }
        }

        private void RefreshDetails()
        {
            TankModuleConfig module = _selectedModule;
            if (module == null)
            {
                _moduleName.text = _noModules;
                _moduleLevel.text = string.Empty;
                _moduleDescription.text = string.Empty;
                _install.gameObject.SetActive(false);
                _upgrade.gameObject.SetActive(false);
                _buyInShop.gameObject.SetActive(false);
                return;
            }

            bool owned = ShopUseCase.IsOwned(_progress, module);
            int level = _progress.GetModuleLevel(module.id);
            _moduleName.text = module.displayName;
            _moduleDescription.text = module.description;
            if (owned)
                _moduleLevel.SetText(_levelFormat, level, module.MaxLevel);
            else
                _moduleLevel.text = _notOwned;

            _buyInShop.gameObject.SetActive(!owned);
            _install.gameObject.SetActive(owned);
            if (owned)
            {
                bool installed = ShopUseCase.IsInstalled(_progress, module);
                _install.interactable = !installed;
                _installLabel.text = installed ? _installedText : _installText;
            }

            int price = owned ? module.UpgradePrice(level) : -1;
            _upgrade.gameObject.SetActive(price >= 0);
            if (price >= 0)
            {
                bool canUpgrade = ShopUseCase.CanUpgrade(_progress, module);
                _upgrade.interactable = canUpgrade;
                _upgradeLabel.SetText(_upgradeFormat, price);
                _upgradeLabel.color = canUpgrade ? UiStyle.Text : UiStyle.Bad;
            }
        }

        // ---------- Stats ----------

        private void RefreshStats()
        {
            TankStats current = TankLoadoutUseCase.Compute(_catalog, _progress);
            TankStats preview = _previewModule != null && !ShopUseCase.IsInstalled(_progress, _previewModule)
                ? TankLoadoutUseCase.Compute(_catalog, _progress, _previewModule)
                : current;

            for (int i = 0; i < TankStats.COUNT; i++)
            {
                float value = current[i];
                float delta = preview[i] - value;
                if (Mathf.Abs(delta) < 0.001f)
                    delta = 0f;

                if (!_statsShown || value != _shownValues[i])
                {
                    _shownValues[i] = value;
                    _statValues[i].SetText(ValueFormats[i], value);
                }

                if (_statsShown && delta == _shownDeltas[i])
                    continue;

                _shownDeltas[i] = delta;
                TMP_Text text = _statDeltas[i];
                if (delta == 0f)
                {
                    text.text = string.Empty;
                    continue;
                }

                // a negative number prints its own minus
                text.SetText(delta > 0f ? PlusFormats[i] : ValueFormats[i], delta);
                bool better = delta > 0f == TankStats.HigherIsBetter(i);
                text.color = better ? UiStyle.Good : UiStyle.Bad;
            }

            _statsShown = true;
        }

        // ---------- Buttons ----------

        private void HandleInstall()
        {
            if (_selectedModule != null)
                ShopUseCase.TryInstall(_progress, _selectedModule); // Changed redraws the screen
        }

        private void HandleUpgrade()
        {
            if (_selectedModule == null)
                return;

            // The button clicks plainly; the result decides the one sound
            bool upgraded = ShopUseCase.TryUpgrade(_progress, _selectedModule);
            AppServices.UiSounds.Play(upgraded ? UiSound.Purchase : UiSound.Error);
        }

        private void HandleShop() => ShopClicked?.Invoke(_slot);
        private void HandleBack() => BackClicked?.Invoke();

        private static Item Clone(Button template, string name)
        {
            template.gameObject.SetActive(false);
            Button button = Instantiate(template, template.transform.parent);
            button.name = name;
            button.gameObject.SetActive(true);
            Transform root = button.transform;
            return new Item
            {
                Button = button,
                Marker = root.Find(ITEM_MARKER).gameObject,
                Name = root.Find(ITEM_NAME).GetComponent<TMP_Text>(),
                Value = root.Find(ITEM_VALUE).GetComponent<TMP_Text>()
            };
        }

        private sealed class Item
        {
            public Button Button;
            public GameObject Marker;
            public TMP_Text Name;
            public TMP_Text Value;
        }
    }
}
