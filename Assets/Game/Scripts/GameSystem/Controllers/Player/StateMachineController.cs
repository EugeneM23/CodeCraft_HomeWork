using UnityEngine;

namespace Gameplay
{
    public class StateMachineController : ITickable, IInitializeble, IDisposable
    {
        private Rigidbody2D _rigidbody2D;
        private InputReader _inputReader;
        private CollisionComponent _collisionComponent;
        private StateMachine _stateMachine;

        [Inject]
        private void Construct(
            CollisionComponent collisionComponent,
            StateMachine stateMachine,
            Rigidbody2D rigidbody2D,
            InputReader inputReader
        )
        {
            _rigidbody2D = rigidbody2D;
            _inputReader = inputReader;
            _collisionComponent = collisionComponent;
            _stateMachine = stateMachine;
        }

        public void Initialize()
        {
            _inputReader.OnFire += Attack;
            _inputReader.OnPushUp += PushUp;
            _inputReader.OnPushSide += PushSide;
        }

        public void Dispose()
        {
            _inputReader.OnFire -= Attack;
            _inputReader.OnPushUp -= PushUp;
            _inputReader.OnPushSide -= PushSide;
        }

        private void PushSide() => _stateMachine.SetState<PushAbilitySideState>();

        private void PushUp()
        {
            Debug.Log("Pushing up");
            _stateMachine.SetState<PushAbilityUPState>();
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