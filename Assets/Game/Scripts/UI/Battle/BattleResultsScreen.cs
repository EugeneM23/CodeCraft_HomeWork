using System;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The end of the battle: victory or defeat, the level's goal, time, kills, reward. Again, To hangar and
    /// Next level (shown only when there is an unlocked next level). Replaces the HUD; Back does not close it.
    /// </summary>
    public sealed class BattleResultsScreen : UIScreen
    {
        public event Action AgainClicked;
        public event Action HangarClicked;
        public event Action NextClicked;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _goal;
        [SerializeField] private TMP_Text _time;
        [SerializeField] private TMP_Text _kills;
        [SerializeField] private TMP_Text _reward;
        [SerializeField] private Button _again;
        [SerializeField] private Button _hangar;
        [SerializeField] private Button _next;

        /// <summary>Fills the screen before it is shown. level may be null (the scene was started without one).</summary>
        public void Setup(BattleResult result, LevelConfig level, int killTarget, bool hasNextLevel)
        {
            _title.text = result.Victory ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
            _title.color = result.Victory ? UiStyle.Good : UiStyle.Bad;

            _goal.text = killTarget > 0 ? $"Цель: уничтожить {killTarget} Regax" : "Цель: выжить";
            if (level != null)
                _goal.text = $"{level.displayName}. {_goal.text}";

            _time.text = "Время: " + ClockTextUseCase.Format(result.Time);
            _kills.text = killTarget > 0 ? $"Уничтожено Regax: {result.Kills}/{killTarget}" : $"Уничтожено Regax: {result.Kills}";
            _reward.text = result.Reward > 0 ? $"+{result.Reward} кредитов" : "Награды нет";
            _reward.color = result.Reward > 0 ? UiStyle.Accent : UiStyle.TextMuted;

            _next.gameObject.SetActive(hasNextLevel);
            FirstSelected = hasNextLevel && result.Victory ? _next : _again;
        }

        private void OnEnable()
        {
            _again.onClick.AddListener(HandleAgain);
            _hangar.onClick.AddListener(HandleHangar);
            _next.onClick.AddListener(HandleNext);
        }

        private void OnDisable()
        {
            _again.onClick.RemoveListener(HandleAgain);
            _hangar.onClick.RemoveListener(HandleHangar);
            _next.onClick.RemoveListener(HandleNext);
        }

        private void HandleAgain() => AgainClicked?.Invoke();
        private void HandleHangar() => HangarClicked?.Invoke();
        private void HandleNext() => NextClicked?.Invoke();
    }
}
