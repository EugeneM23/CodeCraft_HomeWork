using UnityEngine;

namespace Modules.PlayerController
{
    internal class GravityComponent : IVelocity
    {
        private readonly CharacterController2D _character;

        public GravityComponent(CharacterController2D character)
        {
            _character = character;
        }

        public Vector2 GetVelocity()
        {
            if (_character.IsOnStairs  || _character.IsWallSliding)
                return Vector2.zero;

            if (_character.IsCeilingHit)
                return new Vector2(0, 0);

            float gravity = _character.Stats.FallAcceleration;
            float currentYVelocity = _character.Velocity.y;

            float newYVelocity =
                Mathf.MoveTowards(currentYVelocity, -_character.Stats.MaxFallSpeed, gravity * Time.fixedDeltaTime);

            return new Vector2(0, newYVelocity);
        }
    }
}