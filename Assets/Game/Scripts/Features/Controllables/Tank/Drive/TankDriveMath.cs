using UnityEngine;

namespace Game
{
    public static class TankDriveMath
    {
        /// <summary>
        /// Moves a speed one step towards the wanted one. Gaining speed in the direction already
        /// travelled uses <paramref name="acceleration"/>; letting off, or asking for the opposite
        /// direction, uses <paramref name="braking"/>, and a reversal stops at zero first instead
        /// of swinging straight through it in one step.
        /// </summary>
        public static float Approach(float current, float target, float acceleration, float braking, float deltaTime)
        {
            bool reversing = current != 0f && target != 0f && Mathf.Sign(target) != Mathf.Sign(current);

            if (reversing)
                return Mathf.MoveTowards(current, 0f, braking * deltaTime);

            float rate = Mathf.Abs(target) > Mathf.Abs(current) ? acceleration : braking;
            return Mathf.MoveTowards(current, target, rate * deltaTime);
        }
    }
}
