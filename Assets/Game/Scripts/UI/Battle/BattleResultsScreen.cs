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

        /// <summary>
        /// Fills the screen before it is shown (once per battle; the screen may not have woken yet, so texts are
        /// fetched here). levelName may be empty (the scene was started without a level).
        /// </summary>
        public void Setup(BattleResult result, string levelName, int killTarget, bool hasNextLevel)
        {
            _title.text = UiStrings.Get(result.Victory ? "results.victory" : "results.defeat");
            _title.color = result.Victory ? UiStyle.Good : UiStyle.Bad;

            string goal = killTarget > 0
                ? string.Format(UiStrings.Get("goal.kill_format"), killTarget)
                : UiStrings.Get("goal.survive");
            _goal.text = !string.IsNullOrEmpty(levelName)
                ? string.Format(UiStrings.Get("results.goal_with_level_format"), levelName, goal)
                : goal;

            _time.text = string.Format(UiStrings.Get("results.time_format"), ClockTextUseCase.Format(result.Time));
            _kills.text = killTarget > 0
                ? string.Format(UiStrings.Get("results.kills_target_format"), result.Kills, killTarget)
                : string.Format(UiStrings.Get("results.kills_format"), result.Kills);
            _reward.text = result.Reward > 0
                ? string.Format(UiStrings.Get("results.reward_format"), result.Reward)
                : UiStrings.Get("results.no_reward");
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
