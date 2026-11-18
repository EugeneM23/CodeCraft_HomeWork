using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank's numbers as «было → стало»: a value per stat and, when a preview differs, "→ new" in green
    /// (better) or red (worse). Rewrites a text only when its number changed.
    /// </summary>
    public sealed class HangarStatsView : MonoBehaviour
    {
        // TMP SetText formats by HangarTankStats index ({0:N} = N decimals)
        private static readonly string[] ValueFormats = { "{0:0}", "{0:1}", "{0:0}", "{0:1}" };
        private static readonly string[] PreviewFormats = { "→ {0:0}", "→ {0:1}", "→ {0:0}", "→ {0:1}" };

        [Tooltip("Value texts in HangarTankStats order: health, speed, cannon damage, reload")]
        [SerializeField] private TMP_Text[] _values = new TMP_Text[HangarTankStats.COUNT];
        [Tooltip("Preview texts in the same order")]
        [SerializeField] private TMP_Text[] _previews = new TMP_Text[HangarTankStats.COUNT];

        private readonly float[] _shownValues = new float[HangarTankStats.COUNT];
        private readonly float[] _shownPreviews = new float[HangarTankStats.COUNT];
        private bool _shown;

        public void Show(HangarTankStats current) => Show(current, current);

        public void Show(HangarTankStats current, HangarTankStats preview)
        {
            for (int i = 0; i < HangarTankStats.COUNT; i++)
            {
                float value = current[i];
                float next = preview[i];
                if (Mathf.Abs(next - value) < 0.01f)
                    next = value;

                bool valueChanged = !_shown || value != _shownValues[i];
                if (valueChanged)
                {
                    _shownValues[i] = value;
                    _values[i].SetText(ValueFormats[i], value);
                }

                if (!valueChanged && next == _shownPreviews[i])
                    continue;

                _shownPreviews[i] = next;
                TMP_Text text = _previews[i];
                if (next == value)
                {
                    text.text = string.Empty;
                    continue;
                }

                text.SetText(PreviewFormats[i], next);
                text.color = next > value == HangarTankStats.HigherIsBetter(i) ? UiStyle.Good : UiStyle.Bad;
            }

            _shown = true;
        }
    }
}
