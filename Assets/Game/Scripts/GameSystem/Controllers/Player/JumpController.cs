using Game.Scripts.GameObject.Player;
using UnityEngine;

namespace Gameplay
{
    public class JumpController : IInitializeble, IDisposable
    {
        [Inject] private readonly InputReader _inputReader;
        private readonly CollisionComponent _collisionComponent;
        private readonly ImpulseComponent _impulseComponent;
        private readonly JumpComponent _jumpComponent;

        public JumpController(
            CollisionComponent collisionComponent,
            ImpulseComponent impulseComponent,
            JumpComponent jumpComponent
        )
        {
            _collisionComponent = collisionComponent;
            _impulseComponent = impulseComponent;
            _jumpComponent = jumpComponent;
        }

        public void Initialize()
        {
            _jumpComponent.OnJump += Jump;
            _inputReader.OnJump += _jumpComponent.TryJump;
            _collisionComponent.OnGrounded += _jumpComponent.RestJumps;
        }

        public void Dispose()
        {
            _jumpComponent.OnJump -= Jump;
            _inputReader.OnJump -= _jumpComponent.TryJump;
            _collisionComponent.OnGrounded -= _jumpComponent.RestJumps;
        }

        private void Jump()
        {
            _impulseComponent.AddForce(Vector2.up, 40f);
        }
    }
}