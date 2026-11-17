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
        private const string CREDITS_FORMAT = "Кредиты {0}";

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _credits;

        private IPlayerProgress _progress;
        private int _shownCredits = int.MinValue;

        public string Title
        {
            get => _title.text;
            set => _title.text = value;
        }

        private void Awake() => _progress = AppServices.Progress;

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
            _credits.SetText(CREDITS_FORMAT, credits);
        }
    }
}
