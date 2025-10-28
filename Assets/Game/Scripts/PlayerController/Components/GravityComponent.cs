using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class GravityComponent : IVelocity
    {
        private readonly PlayerController _player;

        private Vector2 _impulse;
        private bool _impulseApplied;

        public GravityComponent(PlayerController player)
        {
            _player = player;
            _impulse = Vector2.zero;
            _impulseApplied = false;

            _player.OnHit += ResetImpulse;
        }

        public void AddImpulse(Vector2 impulseValue)
        {
            _player.Restvelocity();
            _impulse = impulseValue;
            _impulseApplied = false;
        }

        private void ResetImpulse()
        {
            _impulse = Vector2.zero;
            _impulseApplied = false;
        }


        public Vector2 GetVelocity()
        {
            if (_player.IsOnStairs)
                return Vector2.zero;

            if (_player.IsCeilingHit)
                return new Vector2(0, -5);

            float gravity = _player.Stats.FallAcceleration;
            float currentYVelocity = _player.Velocity.y;

            if (!_impulseApplied && _impulse != Vector2.zero)
            {
                currentYVelocity += _impulse.y;
                _impulseApplied = true;
            }

            float newYVelocity =
                Mathf.MoveTowards(currentYVelocity, -_player.Stats.MaxFallSpeed, gravity * Time.fixedDeltaTime);

            float horizontalComponent = (!_impulseApplied || _impulse.x != 0) ? _impulse.x : 0;

            return new Vector2(horizontalComponent, newYVelocity);
        }
    }
}