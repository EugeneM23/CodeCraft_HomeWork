using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The top strip of the menu screens: the screen title, marks and samples (IHangarSource). The numbers are
    /// rewritten only when they change.
    /// </summary>
    public sealed class CurrencyBar : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _marks;
        [SerializeField] private TMP_Text _samples;

        private IHangarSource _source;
        private string _marksFormat;
        private string _samplesFormat;
        private int _shownMarks = int.MinValue;
        private int _shownSamples = int.MinValue;

        /// <summary>The screen title: a static label (UiLocalizedText), its key set by the prefab builder.</summary>
        public TMP_Text TitleText => _title;

        private void Awake()
        {
            _source = AppServices.Hangar;
            _marksFormat = UiStrings.Get("hangar.marks_format");
            _samplesFormat = UiStrings.Get("hangar.samples_format");
        }

        private void OnEnable()
        {
            _source.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => _source.Changed -= Refresh;

        private void Refresh()
        {
            int marks = _source.Marks;
            if (marks != _shownMarks)
            {
                _shownMarks = marks;
                _marks.SetText(_marksFormat, marks);
            }

            int samples = _source.Samples;
            if (samples != _shownSamples)
            {
                _shownSamples = samples;
                _samples.SetText(_samplesFormat, samples);
            }
        }
    }
}
