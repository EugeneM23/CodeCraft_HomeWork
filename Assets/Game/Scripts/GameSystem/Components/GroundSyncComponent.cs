using Game.Scripts.Player;
using UnityEngine;

namespace Gameplay
{
    public class GroundSyncComponent : IFixedTickable, IInitializeble, IDisposable
    {
        private CollisionComponent _collision;
        private IMovabele _move;
        private Vector3 _lastPlatformPos;

        [Inject]
        public void Construct(CollisionComponent collision, IMovabele move)
        {
            _collision = collision;
            _move = move;
        }

        public void Initialize()
        {
            _collision.OnGrounded += OnGrounded;
        }

        public void Dispose() => _collision.OnGrounded -= OnGrounded;

        public void FixedTick()
        {
            if (_collision.IsGrounded && _collision.LastHit.collider != null)
            {
                Vector3 newPos = _collision.LastHit.collider.bounds.center;
                Vector3 delta = newPos - _lastPlatformPos;
                _move.AddGroundMove(delta);
                _lastPlatformPos = newPos;
            }
        }

        private void OnGrounded()
        {
            _lastPlatformPos = _collision.LastHit.collider.bounds.center;
        }
    }
}