using System;
using System.Collections.Generic;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// Chapter select: the chapters (open / locked / completed) on the left; the selected one's record (farthest
    /// arena n/N, best time mm:ss), first-completion reward and recommended tank on the right, and To battle.
    /// Start only raises StartClicked: MenuFlow starts the chapter. Texts are rebuilt on open, a click or a change.
    /// </summary>
    public sealed class ChapterSelectScreen : UIScreen
    {
        public event Action<string> StartClicked;
        public event Action BackClicked;

        [Tooltip("Switched-off chapter item; cloned as needed")]
        [SerializeField] private HangarItemView _itemTemplate;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _state;
        [SerializeField] private TMP_Text _record;
        [SerializeField] private TMP_Text _reward;
        [SerializeField] private TMP_Text _tank;
        [SerializeField] private Button _start;
        [SerializeField] private Button _back;

        private readonly List<HangarItemView> _items = new();
        private IHangarSource _source;
        private int _selected = -1;

        private void Awake() => _source = AppServices.Hangar;

        private void OnEnable()
        {
            _start.onClick.AddListener(HandleStart);
            _back.onClick.AddListener(HandleBack);
            _source.Changed += Refresh;
            _selected = -1;
            Refresh();
        }

        private void OnDisable()
        {
            _start.onClick.RemoveListener(HandleStart);
            _back.onClick.RemoveListener(HandleBack);
            _source.Changed -= Refresh;
        }

        private void Refresh()
        {
            IReadOnlyList<HangarChapter> chapters = _source.Chapters;
            if (_selected < 0 || _selected >= chapters.Count)
                _selected = DefaultChapter(chapters);

            while (_items.Count < chapters.Count)
            {
                int index = _items.Count;
                HangarItemView item = _itemTemplate.Clone("Chapter_" + index);
                item.Button.onClick.AddListener(() => Select(index)); // once per item, at creation
                _items.Add(item);
            }

            for (int i = 0; i < _items.Count; i++)
            {
                HangarItemView item = _items[i];
                bool used = i < chapters.Count;
                item.gameObject.SetActive(used);
                if (!used)
                    continue;

                HangarChapter chapter = chapters[i];
                item.SetSelected(i == _selected);
                item.Title.text = string.Format(UiStrings.Get("hangar.chapter.item_format"), i + 1,
                    HangarLook.ChapterName(chapter.Id));
                item.Info.text = StateText(chapter);
                item.Info.color = StateColor(chapter);
            }

            RefreshDetails(chapters);
        }

        private void RefreshDetails(IReadOnlyList<HangarChapter> chapters)
        {
            bool any = _selected >= 0 && _selected < chapters.Count;
            _start.gameObject.SetActive(any);
            if (!any)
            {
                _name.text = string.Empty;
                _state.text = string.Empty;
                _record.text = string.Empty;
                _reward.text = string.Empty;
                _tank.text = string.Empty;
                return;
            }

            HangarChapter chapter = chapters[_selected];
            _name.text = HangarLook.ChapterName(chapter.Id);
            _state.text = StateText(chapter);
            _state.color = StateColor(chapter);
            _record.text = chapter.BestArena > 0
                ? string.Format(UiStrings.Get("hangar.chapter.record_format"), chapter.BestArena, chapter.ArenaCount,
                    chapter.BestTime > 0f ? TimeText(chapter.BestTime) : "—")
                : UiStrings.Get("hangar.chapter.no_record");

            string reward = chapter.FirstRewardSamples > 0
                ? string.Format(UiStrings.Get("hangar.price_both_format"), chapter.FirstRewardMarks, chapter.FirstRewardSamples)
                : string.Format(UiStrings.Get("hangar.price_marks_format"), chapter.FirstRewardMarks);
            _reward.text = string.Format(
                UiStrings.Get(chapter.Completed ? "hangar.chapter.reward_taken_format" : "hangar.chapter.reward_format"), reward);
            _tank.text = string.Format(UiStrings.Get("hangar.chapter.tank_format"), HangarLook.TankName(chapter.RecommendedTankId));
            _start.interactable = chapter.Unlocked;
        }

        private static int DefaultChapter(IReadOnlyList<HangarChapter> chapters)
        {
            // The first open chapter not completed yet, else the first one
            for (int i = 0; i < chapters.Count; i++)
                if (chapters[i].Unlocked && !chapters[i].Completed)
                    return i;
            return chapters.Count > 0 ? 0 : -1;
        }

        private static string StateText(HangarChapter chapter) => UiStrings.Get(
            !chapter.Unlocked ? "hangar.chapter.locked" : chapter.Completed ? "hangar.chapter.completed" : "hangar.chapter.open");

        private static Color StateColor(HangarChapter chapter) =>
            !chapter.Unlocked ? UiStyle.Bad : chapter.Completed ? UiStyle.Good : UiStyle.Accent;

        private static string TimeText(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return string.Format("{0:00}:{1:00}", total / 60, total % 60);
        }

        private void Select(int index)
        {
            _selected = index;
            Refresh();
        }

        private void HandleStart()
        {
            IReadOnlyList<HangarChapter> chapters = _source.Chapters;
            if (_selected >= 0 && _selected < chapters.Count && chapters[_selected].Unlocked)
                StartClicked?.Invoke(chapters[_selected].Id);
        }

        private void HandleBack() => BackClicked?.Invoke();
    }
}
