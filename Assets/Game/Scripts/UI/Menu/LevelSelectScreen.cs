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

        private const int MAX_DIFFICULTY = 3;

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
        private LevelCatalog _catalog;
        private LevelDesignConfig _design;
        private IPlayerProgress _progress;
        private int _selected = -1;

        // Texts from UiStrings, fetched once in Awake
        private string _difficultyFormat;
        private string _cardRewardFormat;
        private string _goalKillFormat;
        private string _goalSurvive;
        private string _rewardFormat;
        private string _unlockAfterFormat;
        private string _locked;

        /// <summary>Called by MenuFlow before the screen is first shown.</summary>
        public void Init(LevelCatalog catalog, LevelDesignConfig design)
        {
            _catalog = catalog;
            _design = design;
            _progress = AppServices.Progress;
        }

        public LevelConfig SelectedLevel => _selected >= 0 && _selected < _cards.Count ? _cards[_selected].Level : null;

        private void Awake()
        {
            _difficultyFormat = UiStrings.Get("level.difficulty_format");
            _cardRewardFormat = UiStrings.Get("level.card_reward_format");
            _goalKillFormat = UiStrings.Get("goal.kill_format");
            _goalSurvive = UiStrings.Get("goal.survive");
            _rewardFormat = UiStrings.Get("level.reward_format");
            _unlockAfterFormat = UiStrings.Get("level.unlock_after_format");
            _locked = UiStrings.Get("level.locked");
        }

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
            for (int i = 0; i < _catalog.Levels.Count; i++)
            {
                LevelConfig level = _catalog.Levels[i];
                if (level == null)
                    continue;

                Button button = Instantiate(_cardTemplate, parent);
                button.name = "Card_" + level.Id;
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
                card.Name.text = level.DisplayName;
                card.Difficulty.text = string.Format(_difficultyFormat, Mathf.Clamp(Difficulty(level), 1, MAX_DIFFICULTY),
                    MAX_DIFFICULTY);
                card.Reward.SetText(_cardRewardFormat, Reward(level));
                _cards.Add(card);
            }
        }

        private void RefreshCards()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                Card card = _cards[i];
                bool unlocked = LevelUnlockUseCase.IsUnlocked(card.Level, _progress);
                card.Lock.gameObject.SetActive(!unlocked);
                card.Name.color = unlocked ? UiStyle.Text : UiStyle.TextMuted;
            }
        }

        private int FirstUnlocked()
        {
            for (int i = 0; i < _cards.Count; i++)
                if (LevelUnlockUseCase.IsUnlocked(_cards[i].Level, _progress))
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

            bool unlocked = LevelUnlockUseCase.IsUnlocked(level, _progress);
            _detailName.text = level.DisplayName;
            _detailDescription.text = level.Description ?? string.Empty;
            int killTarget = KillTarget(level);
            if (killTarget > 0)
                _detailGoal.SetText(_goalKillFormat, killTarget);
            else
                _detailGoal.text = _goalSurvive;
            _detailReward.SetText(_rewardFormat, Reward(level));
            _detailLock.gameObject.SetActive(!unlocked);
            if (!unlocked)
                _detailLock.text = level.RequiredLevel != null
                    ? string.Format(_unlockAfterFormat, level.RequiredLevel.DisplayName)
                    : _locked;
            _start.interactable = unlocked;
        }

        private void HandleStart()
        {
            LevelConfig level = SelectedLevel;
            if (level != null && LevelUnlockUseCase.IsUnlocked(level, _progress))
                StartClicked?.Invoke(level);
        }

        // Design fields by level id; defaults when the level has no entry
        private int Difficulty(LevelConfig level) =>
            _design != null ? _design.Difficulty(level) : LevelDesignConfig.DEFAULT_DIFFICULTY;

        private int KillTarget(LevelConfig level) =>
            _design != null ? _design.KillTarget(level) : LevelDesignConfig.DEFAULT_KILL_TARGET;

        private int Reward(LevelConfig level) =>
            _design != null ? _design.Reward(level) : LevelDesignConfig.DEFAULT_REWARD;

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
