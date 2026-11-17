using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The top strip of the menu screens: the screen title and the player's credits. The credits text is
    /// rewritten only when the balance changes.
    /// </summary>
    public sealed class CurrencyBar : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _credits;

        private IPlayerProgress _progress;
        private string _creditsFormat;
        private int _shownCredits = int.MinValue;

        /// <summary>The screen title: a static label (UiLocalizedText), its key set by the prefab builder.</summary>
        public TMP_Text TitleText => _title;

        private void Awake()
        {
            _progress = AppServices.Progress;
            _creditsFormat = UiStrings.Get("common.credits_format");
        }

        private void OnEnable()
        {
            _progress.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => _progress.Changed -= Refresh;

        private void Refresh()
        {
            int credits = _progress.Credits;
            if (credits == _shownCredits)
                return;

            _shownCredits = credits;
            _credits.SetText(_creditsFormat, credits);
        }
    }
}
