using System;
using System.Collections.Generic;
using ScreenNavigation;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The pause over the battle (GDD §11): Continue, Settings, Exit to hangar; on the right the run's skills
    /// with their levels (a pool of 10 rows). BattleFlow decides what the buttons do.
    /// </summary>
    public sealed class PauseScreen : UIScreen
    {
        public event Action ResumeClicked;
        public event Action SettingsClicked;
        public event Action ExitClicked;

        [SerializeField] private Button _resume;
        [SerializeField] private Button _settings;
        [SerializeField] private Button _exit;

        [Header("Run skills")]
        [SerializeField] private SkillIconView[] _skillRows;
        [SerializeField] private GameObject _noSkills;

        /// <summary>Fills the skill list before the pause is pushed (not per frame).</summary>
        public void ShowSkills(IReadOnlyList<RunSkill> skills)
        {
            int count = skills != null ? Mathf.Min(skills.Count, _skillRows.Length) : 0;
            for (int i = 0; i < _skillRows.Length; i++)
            {
                if (i < count)
                    _skillRows[i].Show(skills[i]);
                else
                    _skillRows[i].Hide();
            }
            _noSkills.SetActive(count == 0);
        }

        private void OnEnable()
        {
            _resume.onClick.AddListener(HandleResume);
            _settings.onClick.AddListener(HandleSettings);
            _exit.onClick.AddListener(HandleExit);
        }

        private void OnDisable()
        {
            _resume.onClick.RemoveListener(HandleResume);
            _settings.onClick.RemoveListener(HandleSettings);
            _exit.onClick.RemoveListener(HandleExit);
        }

        private void HandleResume() => ResumeClicked?.Invoke();
        private void HandleSettings() => SettingsClicked?.Invoke();
        private void HandleExit() => ExitClicked?.Invoke();
    }
}
