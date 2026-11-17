using UnityEngine;

namespace Game
{
    /// <summary>
    /// Tells when one foot comes down. Watches the foot's height above the character's root and
    /// remembers the lowest it has been (slowly forgetting, so a slope or a crouch does not stick):
    /// once the foot has risen clearly above that and comes back down close to it, that is a step.
    /// Small wobbles of a planted foot are not.
    /// </summary>
    public sealed class FootPlant
    {
        /// <summary>A foot this far (m) over its lowest is off the ground. Scaled with the body.</summary>
        private const float LIFT = 0.06f;

        /// <summary>A lifted foot back within this (m) of its lowest has landed. Scaled with the body.</summary>
        private const float PLANT = 0.03f;

        /// <summary>How fast (m/s) the remembered lowest point creeps back up.</summary>
        private const float FORGET = 0.05f;

        private readonly float _lift;
        private readonly float _plant;
        private readonly float _forget;

        private float _lowest = float.MaxValue;
        private bool _down = true;

        /// <param name="scale">The body's size against a person's (Animator.humanScale).</param>
        public FootPlant(float scale)
        {
            _lift = LIFT * scale;
            _plant = PLANT * scale;
            _forget = FORGET * scale;
        }

        /// <summary>Feeds this frame's height; true on the frame the foot lands.</summary>
        public bool Step(float height, float deltaTime)
        {
            _lowest = Mathf.Min(height, _lowest + _forget * deltaTime);

            if (_down)
            {
                if (height > _lowest + _lift)
                    _down = false;
                return false;
            }

            if (height < _lowest + _plant)
            {
                _down = true;
                return true;
            }

            return false;
        }
    }
}
