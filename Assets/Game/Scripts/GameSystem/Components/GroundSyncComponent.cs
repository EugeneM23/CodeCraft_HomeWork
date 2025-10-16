using Game.Scripts.Player;
using UnityEngine;

namespace Gameplay
{
    public class GroundSyncComponent : IFixedTickable, IInitializeble, IDisposable
    {
        private readonly CollisionComponent _collision;
        private readonly MoveComponent _move;

        private Collider2D _lastPlatform;
        private Vector3 _lastPlatformPos;

        public GroundSyncComponent(CollisionComponent collision, MoveComponent move)
        {
            _collision = collision;
            _move = move;
        }

        public void Initialize() => _collision.OnGrounded += OnGrounded;

        public void Dispose() => _collision.OnGrounded -= OnGrounded;

        public void FixedTick()
        {
            if (_collision.IsGrounded && _collision.LastHit.collider != null)
            {
                Collider2D currentPlatform = _collision.LastHit.collider;

                if (_lastPlatform != currentPlatform)
                {
                    _lastPlatform = currentPlatform;
                    _lastPlatformPos = currentPlatform.transform.position;
                    return;
                }

                Vector3 platformPos = currentPlatform.transform.position;
                Vector3 delta = platformPos - _lastPlatformPos;
                delta.y = 0;

                _move.AddGroundMove(delta);

                _lastPlatformPos = platformPos;
            }
        }

        private void OnGrounded()
        {
            _lastPlatformPos = _collision.LastHit.collider.bounds.center;
        }
    }
}