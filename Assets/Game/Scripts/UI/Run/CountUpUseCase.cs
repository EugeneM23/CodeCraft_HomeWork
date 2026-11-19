using UnityEngine;

namespace Game
{
    /// <summary>
    /// The results screen's counting up: a number runs from 0 to its target over a duration, fast first and
    /// slowing to the end (ease-out), whole numbers only, exactly the target at the end.
    /// </summary>
    public static class CountUpUseCase
    {
        public static int Value(int target, float elapsed, float duration)
        {
            if (target <= 0 || duration <= 0f || elapsed >= duration)
                return Mathf.Max(0, target);
            if (elapsed <= 0f)
                return 0;

            float t = elapsed / duration;
            float eased = 1f - (1f - t) * (1f - t);
            return Mathf.Clamp(Mathf.FloorToInt(target * eased), 0, target);
        }

        public static bool IsDone(float elapsed, float duration) => elapsed >= duration;
    }
}
