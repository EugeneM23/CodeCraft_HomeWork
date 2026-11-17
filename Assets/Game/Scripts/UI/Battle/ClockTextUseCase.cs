using UnityEngine;

namespace Game
{
    /// <summary>
    /// Battle time as "mm:ss": whole seconds rounded down, never negative, at most 99:59. The HUD passes
    /// the minutes and seconds to TMP_Text.SetText("{0:00}:{1:00}") (no string per second); the results
    /// screen, built once, takes the string.
    /// </summary>
    public static class ClockTextUseCase
    {
        private const int MAX_SECONDS = 99 * 60 + 59;

        public static int ShownSeconds(float seconds) => Mathf.Clamp(Mathf.FloorToInt(seconds), 0, MAX_SECONDS);

        public static void Split(int shownSeconds, out int minutes, out int seconds)
        {
            minutes = shownSeconds / 60;
            seconds = shownSeconds % 60;
        }

        public static string Format(float seconds)
        {
            Split(ShownSeconds(seconds), out int minutes, out int rest);
            return string.Format("{0:00}:{1:00}", minutes, rest);
        }
    }
}
