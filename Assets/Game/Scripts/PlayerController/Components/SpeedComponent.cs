using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class SpeedComponent
    {
        private readonly ScriptableStats _stats;
        private readonly PlayerController _player;

        private float _horizontalSpeed;

        public SpeedComponent(ScriptableStats stats, PlayerController player)
        {
            _stats = stats;
            _player = player;
        }

        public float CalculateSpeed(float inputX, bool isGrounded)
        {
            float targetSpeed = inputX * _stats.MaxSpeed;

            float acceleration = isGrounded
                ? (Mathf.Abs(inputX) > 0.01f ? _stats.Acceleration : _stats.GroundDeceleration)
                : (Mathf.Abs(inputX) > 0.01f ? _stats.AirAcceleration : _stats.AirDeceleration);

            _horizontalSpeed = Mathf.MoveTowards(_horizontalSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

            return _horizontalSpeed;
        }
    }
}