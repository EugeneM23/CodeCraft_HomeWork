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

            if (isGrounded)
            {
                return _horizontalSpeed = Mathf.MoveTowards(
                    _horizontalSpeed,
                    targetSpeed,
                    (Mathf.Abs(inputX) > 0.01f ? _stats.Acceleration : _stats.GroundDeceleration) * Time.fixedDeltaTime);
            }
            else
            {
                _horizontalSpeed = _player.Velocity.x;
                return _horizontalSpeed = Mathf.MoveTowards(
                    _horizontalSpeed,
                    targetSpeed,
                    (Mathf.Abs(inputX) > 0.01f ? _stats.AirAcceleration : _stats.AirDeceleration) * Time.fixedDeltaTime);
            }
        }
    }
}