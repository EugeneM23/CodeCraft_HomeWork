using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal class MoveComponent : IMoveComponent
    {
        private PlayerController _character;
        private readonly ScriptableStats _stats;

        public MoveComponent(ScriptableStats stats, PlayerController character)
        {
            _stats = stats;
            _character = character;
        }

        public Vector2 Move(Vector2 direction)
        {
            if (direction.x == 0)
            {
                var deceleration = _character.IsGrounded ? _stats.GroundDeceleration : _stats.AirDeceleration;

                float x = Mathf.MoveTowards(_character.Velocity.x, 0, deceleration * Time.fixedDeltaTime);

                return new Vector2(x, _character.Velocity.y);
            }
            else
            {
                var acceleration = _character.IsGrounded ? _stats.Acceleration : _stats.AirAcceleration;
                float x = Mathf.MoveTowards(_character.Velocity.x, direction.x * _stats.MaxSpeed,
                    acceleration * Time.fixedDeltaTime);

                return new Vector2(x, _character.Velocity.y);
            }
        }
    }
}