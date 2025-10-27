using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class MovingPlatformComponent
    {
        private readonly PlayerController _player;
        private readonly CollisionComponent _collision;
        private readonly ScriptableStats _stats;

        private Transform _currentPlatform;
        private Vector3 _lastPlatformPosition;

        public MovingPlatformComponent(PlayerController player, CollisionComponent collision, ScriptableStats stats)
        {
            _player = player;
            _collision = collision;
            _stats = stats;
        }

        public Vector2 GetPlatformDelta()
        {
            if (!_collision.IsGrounded)
            {
                _currentPlatform = null;
                return Vector2.zero;
            }

            Vector2 rayOrigin = new Vector2(_player.transform.position.x, _player.transform.position.y - 0.05f);

            RaycastHit2D hit = Physics2D.Raycast(
                rayOrigin,
                Vector2.down,
                _stats.GrounderDistance + 2f,
                _stats.PlayerLayer
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