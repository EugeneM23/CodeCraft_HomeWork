using System;
using System.Collections.Generic;
using System.Text;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The shop: a tab per tank slot, a grid of module cards (name, key stats, price, Buy / Bought) and the details
    /// of the selected card. Buy only raises BuyClicked: MenuFlow asks for confirmation and buys. Cards are cloned
    /// from a switched-off template child and reused; texts are rebuilt on a tab switch or a progress change only.
    /// </summary>
    public sealed class ShopScreen : UIScreen
    {
        // Children of the card template, by name (Shop part of MenuUiBuilder)
        public const string CARD_MARKER = "Marker";
        public const string CARD_NAME = "Name";
        public const string CARD_STATS = "Stats";
        public const string CARD_PRICE = "Price";
        public const string CARD_BUY = "Buy";

        private const int CARD_STAT_LINES = 3;

        /// <summary>Stat names, by TankStats index.</summary>
        public static readonly string[] StatNames = { "Прочность", "Броня", "Скорость", "Урон", "Перезарядка", "Темп МГ" };

        private static readonly string[] StatUnits = { "", "", "", "", " с", "/с" };

        public event Action<TankModuleConfig> BuyClicked;
        public event Action BackClicked;

        [Tooltip("Tab buttons by (int)TankSlot")]
        [SerializeField] private Button[] _tabs = new Button[6];
        [Tooltip("Active-tab markers by (int)TankSlot")]
        [SerializeField] private GameObject[] _tabMarkers = new GameObject[6];
        [Tooltip("Switched-off card (children Marker, Name, Stats, Price, Buy); cloned as needed")]
        [SerializeField] private Button _cardTemplate;
        [SerializeField] private TMP_Text _detailName;
        [SerializeField] private TMP_Text _detailDescription;
        [SerializeField] private TMP_Text _detailStats;
        [SerializeField] private TMP_Text _detailPrice;
        [SerializeField] private Button _detailBuy;
        [SerializeField] private TMP_Text _detailBuyLabel;
        [SerializeField] private Button _back;

        private readonly List<Card> _cards = new();
        private readonly List<TankModuleConfig> _slotModules = new();
        private readonly StringBuilder _text = new();
        private UnityEngine.Events.UnityAction[] _tabHandlers;

        private ShopCatalogConfig _catalog;
        private IPlayerProgress _progress;
        private TankSlot _slot;
        private int _selected;

        /// <summary>Called by MenuFlow before the screen is first shown.</summary>
        public void Init(ShopCatalogConfig catalog)
        {
            _catalog = catalog;
            _progress = AppServices.Progress;
        }

        /// <summary>The tab the shop shows next (the hangar opens the shop on its selected slot).</summary>
        public void ShowSlot(TankSlot slot)
        {
            if (_slot == slot)
                return;

            _slot = slot;
            _selected = 0;
            if (isActiveAndEnabled)
                Rebuild();
        }

        private void Awake()
        {
            _tabHandlers = new UnityEngine.Events.UnityAction[_tabs.Length];
            for (int i = 0; i < _tabs.Length; i++)
            {
                TankSlot slot = (TankSlot)i;
                _tabHandlers[i] = () => ShowSlot(slot);
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].onClick.AddListener(_tabHandlers[i]);
            _detailBuy.onClick.AddListener(HandleDetailBuy);
            _back.onClick.AddListener(HandleBack);
            _progress.Changed += Refresh;

            Rebuild();
        }

        private void OnDisable()
        {
            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].onClick.RemoveListener(_tabHandlers[i]);
            _detailBuy.onClick.RemoveListener(HandleDetailBuy);
            _back.onClick.RemoveListener(HandleBack);
            _progress.Changed -= Refresh;
        }

        // A tab switch: the slot's modules into the cards, then the states
        private void Rebuild()
        {
            for (int i = 0; i < _tabMarkers.Length; i++)
                _tabMarkers[i].SetActive(i == (int)_slot);

            _slotModules.Clear();
            for (int i = 0; i < _catalog.modules.Count; i++)
            {
                TankModuleConfig module = _catalog.modules[i];
                if (module != null && module.slot == _slot)
                    _slotModules.Add(module);
            }

            while (_cards.Count < _slotModules.Count)
                _cards.Add(CreateCard(_cards.Count));

            for (int i = 0; i < _cards.Count; i++)
            {
                Card card = _cards[i];
                bool used = i < _slotModules.Count;
                card.Root.gameObject.SetActive(used);
                if (!used)
                    continue;

                TankModuleConfig module = _slotModules[i];
                card.Name.text = module.displayName;
                card.Stats.text = StatsText(module.StatsAt(1), CARD_STAT_LINES);
            }

            if (_selected >= _slotModules.Count)
                _selected = 0;
            Refresh();
        }

        // Credits or ownership changed: prices, buy buttons, the details
        private void Refresh()
        {
            for (int i = 0; i < _slotModules.Count; i++)
            {
                Card card = _cards[i];
                TankModuleConfig module = _slotModules[i];
                bool owned = ShopUseCase.IsOwned(_progress, module);
                bool canBuy = ShopUseCase.CanBuy(_progress, module);
                card.Marker.SetActive(i == _selected);
                card.Price.SetText("{0} кр.", module.price);
                card.Price.color = owned || canBuy ? UiStyle.Text : UiStyle.Bad;
                card.Buy.interactable = canBuy;
                card.BuyLabel.text = owned ? "Куплено" : "Купить";
            }

            RefreshDetails();
        }

        private void RefreshDetails()
        {
            bool any = _selected < _slotModules.Count;
            _detailBuy.gameObject.SetActive(any);
            if (!any)
            {
                _detailName.text = "Нет товаров";
                _detailDescription.text = string.Empty;
                _detailStats.text = string.Empty;
                _detailPrice.text = string.Empty;
                return;
            }

            TankModuleConfig module = _slotModules[_selected];
            bool owned = ShopUseCase.IsOwned(_progress, module);
            bool canBuy = ShopUseCase.CanBuy(_progress, module);
            _detailName.text = module.displayName;
            _detailDescription.text = module.description;
            _text.Clear();
            _text.Append("Уровень 1:\n").Append(StatsText(module.StatsAt(1), TankStats.COUNT));
            if (module.MaxLevel > 1)
                _text.Append("\n\nУровень ").Append(module.MaxLevel).Append(":\n")
                    .Append(StatsText(module.StatsAt(module.MaxLevel), TankStats.COUNT));
            _detailStats.text = _text.ToString();
            _detailPrice.SetText("Цена: {0} кредитов", module.price);
            _detailPrice.color = owned || canBuy ? UiStyle.Text : UiStyle.Bad;
            _detailBuy.interactable = canBuy;
            _detailBuyLabel.text = owned ? "КУПЛЕНО" : "КУПИТЬ";
        }

        private Card CreateCard(int index)
        {
            _cardTemplate.gameObject.SetActive(false);
            Button root = Instantiate(_cardTemplate, _cardTemplate.transform.parent);
            root.name = "Card_" + index;
            root.gameObject.SetActive(true);
            Transform cardRoot = root.transform;
            Button buy = cardRoot.Find(CARD_BUY).GetComponent<Button>();
            Card card = new()
            {
                Root = root,
                Marker = cardRoot.Find(CARD_MARKER).gameObject,
                Name = cardRoot.Find(CARD_NAME).GetComponent<TMP_Text>(),
                Stats = cardRoot.Find(CARD_STATS).GetComponent<TMP_Text>(),
                Price = cardRoot.Find(CARD_PRICE).GetComponent<TMP_Text>(),
                Buy = buy,
                BuyLabel = buy.GetComponentInChildren<TMP_Text>(true)
            };
            // once per card, at creation
            root.onClick.AddListener(() => Select(index));
            buy.onClick.AddListener(() => HandleBuy(index));
            return card;
        }

        private void Select(int index)
        {
            if (index >= _slotModules.Count)
                return;

            _selected = index;
            for (int i = 0; i < _slotModules.Count; i++)
                _cards[i].Marker.SetActive(i == index);
            RefreshDetails();
        }

        private void HandleBuy(int index)
        {
            Select(index);
            HandleDetailBuy();
        }

        private void HandleDetailBuy()
        {
            if (_selected < _slotModules.Count && ShopUseCase.CanBuy(_progress, _slotModules[_selected]))
                BuyClicked?.Invoke(_slotModules[_selected]);
        }

        private void HandleBack() => BackClicked?.Invoke();

        // Nonzero stats, one per line, at most maxLines (built on a tab switch or a click, not per frame)
        private static string StatsText(TankStats stats, int maxLines)
        {
            StringBuilder text = new();
            int lines = 0;
            for (int i = 0; i < TankStats.COUNT && lines < maxLines; i++)
            {
                float value = stats[i];
                if (Mathf.Abs(value) < 0.001f)
                    continue;

                if (lines > 0)
                    text.Append('\n');
                text.Append(StatNames[i]).Append(' ').Append(value.ToString("+0.#;-0.#")).Append(StatUnits[i]);
                lines++;
            }
            return lines > 0 ? text.ToString() : "Без прибавок";
        }

        private sealed class Card
        {
            public Button Root;
            public GameObject Marker;
            public TMP_Text Name;
            public TMP_Text Stats;
            public TMP_Text Price;
            public Button Buy;
            public TMP_Text BuyLabel;
        }
    }
}
