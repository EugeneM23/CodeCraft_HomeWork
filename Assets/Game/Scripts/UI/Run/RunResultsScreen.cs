using System;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The end of a run (GDD §11): victory, death or left to the hangar; arena reached n/5, time, kills, crushes,
    /// elites; marks and samples count up from 0 over about a second (unscaled time, SetText, a tick sound at
    /// most 10 times a second); «Новый рекорд» when the run beat the chapter's farthest arena. To hangar and
    /// Again. Replaces the HUD; Back does not close it.
    /// </summary>
    public sealed class RunResultsScreen : UIScreen
    {
        private const float COUNT_DELAY = 0.35f;
        private const float COUNT_DURATION = 1f;
        private const float TICK_INTERVAL = 0.1f;

        public event Action HangarClicked;
        public event Action AgainClicked;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _arena;
        [SerializeField] private TMP_Text _time;
        [SerializeField] private TMP_Text _kills;
        [SerializeField] private TMP_Text _crushes;
        [SerializeField] private TMP_Text _elites;
        [SerializeField] private TMP_Text _marks;
        [SerializeField] private TMP_Text _samples;
        [SerializeField] private GameObject _record;
        [SerializeField] private Button _hangar;
        [SerializeField] private Button _again;

        private int _marksTarget;
        private int _samplesTarget;
        private int _shownMarks = -1;
        private int _shownSamples = -1;
        private float _startedAt;
        private float _nextTick;
        private bool _counting;

        /// <summary>Fills the screen before it is shown (once per run; texts are fetched here, not per frame).</summary>
        public void Setup(RunSummary summary)
        {
            string titleKey = summary.Victory ? "run.results_victory"
                : summary.Abandoned ? "run.results_abandoned" : "run.results_defeat";
            _title.text = UiStrings.Get(titleKey);
            _title.color = summary.Victory ? UiStyle.Good : UiStyle.Bad;

            _arena.SetText(UiStrings.Get("run.results_arena_format"), summary.ArenaReached, summary.ArenaCount);
            _time.text = ClockTextUseCase.Format(summary.Time);
            _kills.SetText("{0:0}", summary.Kills);
            _crushes.SetText("{0:0}", summary.Crushes);
            _elites.SetText("{0:0}", summary.ElitesKilled);
            _record.SetActive(summary.NewRecord);

            _marksTarget = Mathf.Max(0, summary.Marks);
            _samplesTarget = Mathf.Max(0, summary.Samples);
            _shownMarks = _shownSamples = -1;
            _startedAt = Time.unscaledTime + COUNT_DELAY;
            _nextTick = 0f;
            _counting = true;
            ShowCounts(0, 0, false);

            FirstSelected = _hangar;
        }

        private void OnEnable()
        {
            _hangar.onClick.AddListener(HandleHangar);
            _again.onClick.AddListener(HandleAgain);
        }

        private void OnDisable()
        {
            _hangar.onClick.RemoveListener(HandleHangar);
            _again.onClick.RemoveListener(HandleAgain);
        }

        protected override void Update() // perf-ok: ticks during pause (results count up while the game stands)
        {
            base.Update();
            if (!_counting)
                return;

            float elapsed = Time.unscaledTime - _startedAt;
            int marks = CountUpUseCase.Value(_marksTarget, elapsed, COUNT_DURATION);
            int samples = CountUpUseCase.Value(_samplesTarget, elapsed, COUNT_DURATION);
            ShowCounts(marks, samples, true);
            if (CountUpUseCase.IsDone(elapsed, COUNT_DURATION))
                _counting = false;
        }

        private void ShowCounts(int marks, int samples, bool sound)
        {
            bool marksChanged = marks != _shownMarks;
            bool samplesChanged = samples != _shownSamples;
            if (marksChanged)
            {
                _shownMarks = marks;
                _marks.SetText("+{0:0}", marks);
            }
            if (samplesChanged)
            {
                _shownSamples = samples;
                _samples.SetText("+{0:0}", samples);
            }

            if (!sound || (!marksChanged && !samplesChanged) || Time.unscaledTime < _nextTick)
                return;

            _nextTick = Time.unscaledTime + TICK_INTERVAL;
            AppServices.UiSounds.Play(samplesChanged ? UiSound.SamplesTick : UiSound.MarksTick);
        }

        private void HandleHangar() => HangarClicked?.Invoke();
        private void HandleAgain() => AgainClicked?.Invoke();
    }
}
