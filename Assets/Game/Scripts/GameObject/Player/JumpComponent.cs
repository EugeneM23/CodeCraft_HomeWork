using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject.Player
{
    public class JumpComponent : CompositCondition
    {
        private const int JUMP_COUNT = 2;

        private readonly RigidbodyForceComponent _forceComponent;
        private readonly CollisionComponent _collisionComponent;
        private readonly float _jumpForce;

        [Inject] private readonly Transform _transform;

        private int _jumpCount;

        public JumpComponent(float jumpForce, RigidbodyForceComponent forceComponent,
            CollisionComponent collisionComponent)
        {
            _jumpForce = jumpForce;
            _forceComponent = forceComponent;
            _collisionComponent = collisionComponent;
        }

        public void Jump()
        {
            if (AndCondition() || _jumpCount >= JUMP_COUNT)
                return;

            Vector2 direction = _collisionComponent.GetHitNormal();
            _forceComponent.AddForce(direction, _jumpForce);

            _jumpCount++;
        }

        public void ResetJump() => _jumpCount = 0;
    }
}