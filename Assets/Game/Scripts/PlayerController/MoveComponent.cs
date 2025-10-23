using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal class MoveComponent
    {
        private readonly ScriptableStats _stats;

        public MoveComponent(ScriptableStats stats)
        {
            _stats = stats;
        }

        public float Move(float moveInput, bool isGrounded, float currentXVelocity)
        {
            if (moveInput == 0)
            {
                var deceleration = isGrounded
                    ? _stats.GroundDeceleration
                    : _stats.AirDeceleration;

                return Mathf.MoveTowards(currentXVelocity, 0, deceleration * Time.fixedDeltaTime);
            }
            else
            {
                return Mathf.MoveTowards(currentXVelocity,
                    moveInput * _stats.MaxSpeed,
                    _stats.Acceleration * Time.fixedDeltaTime);
            }
        }
    }
}