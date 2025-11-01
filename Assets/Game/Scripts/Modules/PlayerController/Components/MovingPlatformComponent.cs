using Gameplay;
using UnityEngine;

namespace Modules.PlayerController
{
    internal class MovingPlatformComponent : IVelocity
    {
        private readonly PlayerController _player;

        private Transform _currentPlatform;
        private Vector3 _lastPlatformPosition;

        public MovingPlatformComponent(PlayerController player) => _player = player;

        public Vector2 GetVelocity()
        {
            if (!_player.IsGrounded)
            {
                _currentPlatform = null;
                return Vector2.zero;
            }

            Vector2 rayOrigin = new Vector2(_player.Collider.bounds.center.x, _player.Collider.bounds.center.y);

            RaycastHit2D hit = Physics2D.Raycast(
                rayOrigin,
                Vector2.down,
                _player.Stats.GrounderDistance,
                _player.Stats.LayerMask
            );
            if (hit.collider != null)
            {
                if (_currentPlatform != hit.collider.transform)
                {
                    _currentPlatform = hit.collider.transform;
                    _lastPlatformPosition = _currentPlatform.position;
                    return Vector2.zero;
                }

                Vector3 currentPos = _currentPlatform.position;
                Vector3 delta3 = currentPos - _lastPlatformPosition;
                _lastPlatformPosition = currentPos;

                Vector2 delta = new Vector2(delta3.x, delta3.y) / Time.fixedDeltaTime;

                return delta;
            }

            _currentPlatform = null;
            return Vector2.zero;
        }
    }
}