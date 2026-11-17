using UnityEngine;

namespace Game
{
    /// <summary>
    /// Tells when a landing helicopter is down. Either the skids reach the ground height, or the
    /// helicopter has stopped coming down for a while although it is meant to go lower — it has
    /// set down on rubble or on top of a wall.
    /// </summary>
    public sealed class TouchdownDetector
    {
        public const float GROUND_TOLERANCE = 0.05f;
        public const float STALL_SPEED = 0.1f;
        public const float STALL_TIME = 0.3f;

        private float _stalled;

        public void Reset() => _stalled = 0f;

        /// <param name="height">Skids above the ground height.</param>
        /// <param name="verticalSpeed">The body's actual vertical speed.</param>
        public bool Step(float height, float verticalSpeed, float deltaTime)
        {
            if (height <= GROUND_TOLERANCE)
                return true;

            _stalled = Mathf.Abs(verticalSpeed) < STALL_SPEED ? _stalled + deltaTime : 0f;
            return _stalled >= STALL_TIME - 1e-4f;
        }
    }
}
