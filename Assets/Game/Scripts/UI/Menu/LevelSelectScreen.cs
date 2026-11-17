using System;
using System.Collections.Generic;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// Level select: a card per level of the catalog (name, difficulty, reward, lock) and the details of the
    /// selected one with Start and To hangar. Cards are cloned from a switched-off template child the first
    /// time the screen is shown and reused afterwards.
    /// </summary>
    public sealed class LevelSelectScreen : UIScreen
    {
        // Children of the card template, by name (LevelSelect part of MenuUiBuilder)
        public const string CARD_MARKER = "Marker";
        public const string CARD_NAME = "Name";
        public const string CARD_DIFFICULTY = "Difficulty";
        public const string CARD_REWARD = "Reward";
        public const string CARD_LOCK = "Lock";

        private static readonly string[] Stars = { "Сложность 1/3", "Сложность 2/3", "Сложность 3/3" };

        public event Action<LevelConfig> StartClicked;
        public event Action HangarClicked;
        public event Action BackClicked;

        [Tooltip("Switched-off card; cloned once per level")]
        [SerializeField] private Button _cardTemplate;
        [SerializeField] private TMP_Text _detailName;
        [SerializeField] private TMP_Text _detailDescription;
        [SerializeField] private TMP_Text _detailGoal;
        [SerializeField] private TMP_Text _detailReward;
        [Tooltip("What unlocks a locked level; hidden for an open one")]
        [SerializeField] private TMP_Text _detailLock;
        [SerializeField] private Button _start;
        [SerializeField] private Button _toHangar;
        [SerializeField] private Button _back;

        private readonly List<Card> _cards = new();
        private LevelCatalogConfig _catalog;
        private IPlayerProgress _progress;
        private int _selected = -1;

        /// <summary>Called by MenuFlow before the screen is first shown.</summary>
        public void Init(LevelCatalogConfig catalog)
        {
            _catalog = catalog;
            _progress = AppServices.Progress;
        }

        public LevelConfig SelectedLevel => _selected >= 0 && _selected < _cards.Count ? _cards[_selected].Level : null;

        private void OnEnable()
        {
            _start.onClick.AddListener(HandleStart);
            _toHangar.onClick.AddListener(HandleHangar);
            _back.onClick.AddListener(HandleBack);

            EnsureCards();
            RefreshCards();
            if (_selected < 0 || _selected >= _cards.Count)
                _selected = FirstUnlocked();
            Select(_selected);
        }

        private void OnDisable()
        {
            _start.onClick.RemoveListener(HandleStart);
            _toHangar.onClick.RemoveListener(HandleHangar);
            _back.onClick.RemoveListener(HandleBack);
        }

        private void EnsureCards()
        {
            if (_catalog == null || _cards.Count > 0)
                return;

            _cardTemplate.gameObject.SetActive(false);
            Transform parent = _cardTemplate.transform.parent;
            for (int i = 0; i < _catalog.levels.Count; i++)
            {
                LevelConfig level = _catalog.levels[i];
                if (level == null)
                    continue;

                Button button = Instantiate(_cardTemplate, parent);
                button.name = "Card_" + level.id;
                button.gameObject.SetActive(true);
                Transform root = button.transform;
                Card card = new()
                {
                    Level = level,
                    Button = button,
                    Marker = root.Find(CARD_MARKER).gameObject,
                    Name = root.Find(CARD_NAME).GetComponent<TMP_Text>(),
                    Difficulty = root.Find(CARD_DIFFICULTY).GetComponent<TMP_Text>(),
                    Reward = root.Find(CARD_REWARD).GetComponent<TMP_Text>(),
                    Lock = root.Find(CARD_LOCK).GetComponent<TMP_Text>()
                };
                int index = _cards.Count;
                button.onClick.AddListener(() => Select(index)); // once per card, at creation
                card.Name.text = level.displayName;
                card.Difficulty.text = Stars[Mathf.Clamp(level.difficulty, 1, Stars.Length) - 1];
                card.Reward.SetText("Награда: {0}", level.reward);
                _cards.Add(card);
            }
        }

        private void RefreshCards()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                Card card = _cards[i];
                bool unlocked = LevelCatalogConfig.IsUnlocked(card.Level, _progress);
                card.Lock.gameObject.SetActive(!unlocked);
                card.Name.color = unlocked ? UiStyle.Text : UiStyle.TextMuted;
            }
        }

        private int FirstUnlocked()
        {
            for (int i = 0; i < _cards.Count; i++)
                if (LevelCatalogConfig.IsUnlocked(_cards[i].Level, _progress))
                    return i;
            return _cards.Count > 0 ? 0 : -1;
        }

        private void Select(int index)
        {
            _selected = index;
            for (int i = 0; i < _cards.Count; i++)
                _cards[i].Marker.SetActive(i == index);

            LevelConfig level = SelectedLevel;
            if (level == null)
            {
                _detailName.text = string.Empty;
                _detailDescription.text = string.Empty;
                _detailGoal.text = string.Empty;
                _detailReward.text = string.Empty;
                _detailLock.gameObject.SetActive(false);
                _start.interactable = false;
                return;
            }

            bool unlocked = LevelCatalogConfig.IsUnlocked(level, _progress);
            _detailName.text = level.displayName;
            _detailDescription.text = level.description;
            if (level.killTarget > 0)
                _detailGoal.SetText("Цель: уничтожить {0} Regax", level.killTarget);
            else
                _detailGoal.text = "Цель: выжить";
            _detailReward.SetText("Награда: {0} кредитов", level.reward);
            _detailLock.gameObject.SetActive(!unlocked);
            if (!unlocked)
                _detailLock.text = level.requiredLevel != null
                    ? "Откроется после победы на уровне «" + level.requiredLevel.displayName + "»"
                    : "Уровень закрыт";
            _start.interactable = unlocked;
        }

        private void HandleStart()
        {
            LevelConfig level = SelectedLevel;
            if (level != null && LevelCatalogConfig.IsUnlocked(level, _progress))
                StartClicked?.Invoke(level);
        }

        private void HandleHangar() => HangarClicked?.Invoke();
        private void HandleBack() => BackClicked?.Invoke();

        private sealed class Card
        {
            public LevelConfig Level;
            public Button Button;
            public GameObject Marker;
            public TMP_Text Name;
            public TMP_Text Difficulty;
            public TMP_Text Reward;
            public TMP_Text Lock;
        }
    }
}
