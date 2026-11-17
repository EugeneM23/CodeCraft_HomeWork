using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>A body that can leap: the elite's jump away from a blast (<see cref="LeapAwayTask"/>).</summary>
    public interface ILeapActuator
    {
        /// <summary>In the air now.</summary>
        bool IsLeaping { get; }

        /// <summary>
        /// Leaps <paramref name="minDistance"/>..<paramref name="maxDistance"/> m away from <paramref name="from"/>, onto
        /// ground it can land on, in <paramref name="duration"/> seconds. False — no such ground around (nothing starts).
        /// </summary>
        bool TryLeapAway(Vector3 from, float minDistance, float maxDistance, float duration);
    }
}
