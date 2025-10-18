using UnityEngine;

namespace Gameplay
{
    public class EnemyStateMachineController : ITickable, IInitializeble, IDisposable
    {
        private Rigidbody2D _rigidbody2D;
        private CollisionComponent _collisionComponent;
        private StateMachine _stateMachine;

        [Inject]
        private void Construct(
            CollisionComponent collisionComponent,
            StateMachine stateMachine,
            Rigidbody2D rigidbody2D
        )
        {
            _rigidbody2D = rigidbody2D;
            _collisionComponent = collisionComponent;
            _stateMachine = stateMachine;
        }

        public void Initialize()
        {
        }

        public void Dispose()
        {
        }

        private void Attack() => _stateMachine.SetState<AttackState>();

        public void Tick()
        {
            if (!_collisionComponent.IsGrounded)
            {
                _stateMachine.SetState<FallState>();
                return;
            }

            float speed = Mathf.Abs(_rigidbody2D.linearVelocity.x);

            if (speed > 1f)
                _stateMachine.SetState<RunState>();
            else
                _stateMachine.SetState<IdleState>();
        }
    }
}