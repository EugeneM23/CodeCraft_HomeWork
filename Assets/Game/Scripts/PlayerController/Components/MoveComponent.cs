using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class MoveComponent : IVelocity
    {
        private readonly PlayerController _player;

        private float _horizontalSpeed;
        private Vector2 _currentVelocity;

        public MoveComponent(PlayerController player) => _player = player;

        public void Move(Vector2 direction)
        {
            float targetSpeed = direction.x * _player.Stats.MaxSpeed;

            _horizontalSpeed = Mathf.MoveTowards(
                _horizontalSpeed,
                targetSpeed,
                _player.Stats.Acceleration * Time.fixedDeltaTime
            );

            Vector2 surfaceNormal = _player.SurfaceNormal;

            if (surfaceNormal == Vector2.zero)
            {
                _currentVelocity = new Vector2(_horizontalSpeed, 0);
                return;
            }

            Vector2 surfaceTangent = new Vector2(surfaceNormal.y, -surfaceNormal.x);
            surfaceTangent.Normalize();

            _currentVelocity = surfaceTangent * _horizontalSpeed;
        }

        public Vector2 GetVelocity() => _currentVelocity;
    }
}