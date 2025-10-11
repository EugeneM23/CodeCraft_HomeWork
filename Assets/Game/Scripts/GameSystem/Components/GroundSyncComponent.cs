using Game.Scripts.Player;
using UnityEngine;

namespace Gameplay
{
    public class GroundSyncComponent : IFixedTickable, IInitializeble
    {
        private CollisionComponent _collision;
        private MoveComponent _move;
        private Vector3 _lastPlatformPos;

        public void Initialize()
        {
            _collision = ServiceLocator.Get<CollisionComponent>(PlayerId.CollisionComponent);
            _move = ServiceLocator.Get<MoveComponent>(PlayerId.MoveComponent);
            _collision.OnGrounded += OnGrounded;
        }

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