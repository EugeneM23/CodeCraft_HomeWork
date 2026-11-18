using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ScreenNavigation;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The shop: tabs Guns / Armor / Engine and tracks / Special / Tanks; module cards with the name, description,
    /// what the module changes against the current tank, the price in marks and samples and the lock condition.
    /// Buy only raises BuyClicked: MenuFlow asks for confirmation and buys. The Tanks tab shows placeholder cards
    /// built by the menu builder ("soon"). Cards are cloned from a switched-off template and reused; texts are
    /// rebuilt on a tab switch or a progress change only.
    /// </summary>
    public sealed class ShopScreen : UIScreen
    {
        public const int TANKS_TAB = 4;

        /// <summary>UiStrings keys of the tab labels, by tab.</summary>
        public static readonly string[] TabKeys =
            { "hangar.shop.tab_guns", "hangar.shop.tab_armor", "hangar.shop.tab_mobility", "hangar.shop.tab_special", "hangar.shop.tab_tanks" };

        // The slots each tab sells, by tab; the Tanks tab has none
        private static readonly ModuleSlot[][] TabSlots =
        {
            new[] { ModuleSlot.Gun, ModuleSlot.MachineGun },
            new[] { ModuleSlot.Armor },
            new[] { ModuleSlot.Engine, ModuleSlot.Tracks },
            new[] { ModuleSlot.Special },
            Array.Empty<ModuleSlot>()
        };

        // UiStrings keys of the stat names, by HangarTankStats index
        private static readonly string[] StatKeys =
            { "hangar.stat.health", "hangar.stat.speed", "hangar.stat.damage", "hangar.stat.reload" };

        public event Action<string> BuyClicked;
        public event Action BackClicked;

        [Tooltip("Tab buttons in TabKeys order")]
        [SerializeField] private Button[] _tabs = new Button[5];
        [Tooltip("Active-tab markers in the same order")]
        [SerializeField] private GameObject[] _tabMarkers = new GameObject[5];
        [Tooltip("Switched-off module card (Title, Info, Extra, Price, Action); cloned as needed")]
        [SerializeField] private HangarItemView _cardTemplate;
        [Tooltip("The module grid; hidden on the Tanks tab")]
        [SerializeField] private GameObject _modulesPanel;
        [Tooltip("Placeholder tank cards; shown on the Tanks tab")]
        [SerializeField] private GameObject _tanksPanel;
        [SerializeField] private Button _back;

        private readonly List<HangarItemView> _cards = new();
        private readonly List<string> _ids = new();
        private readonly StringBuilder _text = new();
        private UnityEngine.Events.UnityAction[] _tabHandlers;
        private IHangarSource _source;
        private int _tab;

        private void Awake()
        {
            _source = AppServices.Hangar;
            _tabHandlers = new UnityEngine.Events.UnityAction[_tabs.Length];
            for (int i = 0; i < _tabs.Length; i++)
            {
                int tab = i;
                _tabHandlers[i] = () => ShowTab(tab);
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].onClick.AddListener(_tabHandlers[i]);
            _back.onClick.AddListener(HandleBack);
            _source.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].onClick.RemoveListener(_tabHandlers[i]);
            _back.onClick.RemoveListener(HandleBack);
            _source.Changed -= Refresh;
        }

        private void ShowTab(int tab)
        {
            if (tab == _tab)
                return;

            _tab = tab;
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < _tabMarkers.Length; i++)
                _tabMarkers[i].SetActive(i == _tab);
            _tanksPanel.SetActive(_tab == TANKS_TAB);
            _modulesPanel.SetActive(_tab != TANKS_TAB);

            _ids.Clear();
            ModuleSlot[] slots = _tab >= 0 && _tab < TabSlots.Length ? TabSlots[_tab] : Array.Empty<ModuleSlot>();
            for (int s = 0; s < slots.Length; s++)
            {
                IReadOnlyList<HangarModule> modules = _source.ModulesFor(HangarSlots.Id(slots[s]));
                for (int i = 0; i < modules.Count; i++)
                    _ids.Add(modules[i].Id);
            }

            while (_cards.Count < _ids.Count)
            {
                int index = _cards.Count;
                HangarItemView card = _cardTemplate.Clone("Card_" + index);
                card.Action.onClick.AddListener(() => HandleBuy(index)); // once per card, at creation
                _cards.Add(card);
            }

            HangarTankStats current = _source.Stats();
            for (int i = 0; i < _cards.Count; i++)
            {
                HangarItemView card = _cards[i];
                bool used = i < _ids.Count;
                card.gameObject.SetActive(used);
                if (used)
                    Draw(card, _source.Module(_ids[i]), current);
            }
        }

        private void Draw(HangarItemView card, HangarModule module, HangarTankStats current)
        {
            card.SetIcon(module.SlotId, module.Id);
            card.Title.text = HangarLook.ModuleName(module.Id);
            card.Info.text = HangarLook.ModuleDescription(module.Id);
            card.Extra.text = StatsText(current, _source.Stats(module.Id));

            PurchaseResult result = _source.CanBuy(module.Id);
            card.Action.gameObject.SetActive(!module.Owned && !module.Locked);
            if (module.Owned)
            {
                card.Price.text = UiStrings.Get("hangar.shop.owned");
                card.Price.color = UiStyle.Good;
                return;
            }

            if (module.Locked)
            {
                card.Price.text = UiStrings.Get(module.LockReasonKey);
                card.Price.color = UiStyle.Bad;
                return;
            }

            HangarLook.SetPrice(card.Price, module.PriceMarks, module.PriceSamples);
            card.Price.color = result == PurchaseResult.Done ? UiStyle.Text : UiStyle.Bad;
            card.Action.interactable = result == PurchaseResult.Done;
            card.ActionLabel.text = result == PurchaseResult.Done ? UiStrings.Get("hangar.shop.buy") : HangarLook.Result(result);
        }

        // "Урон 150 → 250" per changed stat (built on a tab switch or a change, not per frame)
        private string StatsText(HangarTankStats current, HangarTankStats with)
        {
            _text.Clear();
            for (int i = 0; i < HangarTankStats.COUNT; i++)
            {
                if (Mathf.Abs(with[i] - current[i]) < 0.01f)
                    continue;

                if (_text.Length > 0)
                    _text.Append('\n');
                string format = i == HangarTankStats.SPEED || i == HangarTankStats.CANNON_RELOAD ? "0.0" : "0";
                _text.Append(UiStrings.Get(StatKeys[i])).Append(' ')
                    .Append(current[i].ToString(format, CultureInfo.InvariantCulture)).Append(" → ")
                    .Append(with[i].ToString(format, CultureInfo.InvariantCulture));
            }

            return _text.Length > 0 ? _text.ToString() : UiStrings.Get("hangar.shop.no_change");
        }

        private void HandleBuy(int index)
        {
            if (index < _ids.Count && _source.CanBuy(_ids[index]) == PurchaseResult.Done)
                BuyClicked?.Invoke(_ids[index]);
        }

        private void HandleBack() => BackClicked?.Invoke();
    }
}
