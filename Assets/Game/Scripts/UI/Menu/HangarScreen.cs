using System;
using System.Collections.Generic;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The hangar over the 3D tank: six slots on the left (icon, module, level), the tank's numbers on the right,
    /// marks and samples on top, Shop / Upgrade / To battle at the bottom right. The centre stays empty for the
    /// tank. A slot click raises SlotClicked (MenuFlow opens the slot assembly); redraws on IHangarSource.Changed.
    /// </summary>
    public sealed class HangarScreen : UIScreen
    {
        public event Action<string> SlotClicked;
        public event Action ShopClicked;
        public event Action UpgradeClicked;
        public event Action BattleClicked;
        public event Action BackClicked;

        [Tooltip("Switched-off slot item; cloned once per slot")]
        [SerializeField] private HangarItemView _slotTemplate;
        [SerializeField] private HangarStatsView _stats;
        [SerializeField] private TMP_Text _tankName;
        [SerializeField] private Button _shop;
        [SerializeField] private Button _upgrade;
        [SerializeField] private Button _battle;
        [SerializeField] private Button _back;

        private readonly List<HangarItemView> _slots = new();
        private IHangarSource _source;
        private string _levelFormat;

        private void Awake()
        {
            _source = AppServices.Hangar;
            _levelFormat = UiStrings.Get("hangar.level_format");
        }

        private void OnEnable()
        {
            _shop.onClick.AddListener(HandleShop);
            _upgrade.onClick.AddListener(HandleUpgrade);
            _battle.onClick.AddListener(HandleBattle);
            _back.onClick.AddListener(HandleBack);
            _source.Changed += Refresh;

            EnsureSlots();
            Refresh();
        }

        private void OnDisable()
        {
            _shop.onClick.RemoveListener(HandleShop);
            _upgrade.onClick.RemoveListener(HandleUpgrade);
            _battle.onClick.RemoveListener(HandleBattle);
            _back.onClick.RemoveListener(HandleBack);
            _source.Changed -= Refresh;
        }

        private void EnsureSlots()
        {
            if (_slots.Count > 0)
                return;

            IReadOnlyList<string> slots = _source.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                string slot = slots[i];
                HangarItemView item = _slotTemplate.Clone("Slot_" + slot);
                item.Button.onClick.AddListener(() => SlotClicked?.Invoke(slot)); // once per slot, at creation
                item.SetIcon(slot);
                item.Extra.text = HangarLook.SlotName(slot);
                _slots.Add(item);
            }
        }

        private void Refresh()
        {
            _tankName.text = HangarLook.TankName(_source.SelectedTankId);
            IReadOnlyList<string> slots = _source.Slots;
            for (int i = 0; i < _slots.Count && i < slots.Count; i++)
            {
                HangarItemView item = _slots[i];
                string installed = _source.InstalledIn(slots[i]);
                if (installed == null)
                {
                    item.Title.text = UiStrings.Get("hangar.slot_empty");
                    item.Info.text = string.Empty;
                    continue;
                }

                HangarModule module = _source.Module(installed);
                item.Title.text = HangarLook.ModuleName(installed);
                item.Info.SetText(_levelFormat, module.Level, module.MaxLevel);
            }

            _stats.Show(_source.Stats());
        }

        private void HandleShop() => ShopClicked?.Invoke();
        private void HandleUpgrade() => UpgradeClicked?.Invoke();
        private void HandleBattle() => BattleClicked?.Invoke();
        private void HandleBack() => BackClicked?.Invoke();
    }
}
