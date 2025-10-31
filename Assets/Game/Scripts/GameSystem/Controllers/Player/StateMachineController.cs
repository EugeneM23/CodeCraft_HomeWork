using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class StateMachineController : ITickable, IInitializeble, IDisposable
    {
        private PlayerController _player;
        private StateMachine _stateMachine;

        [Inject]
        private void Construct(StateMachine stateMachine, PlayerController player)
        {
            _player = player;
            _stateMachine = stateMachine;
        }

        public void Initialize()
        {
        }

        public void Dispose()
        {
        }

        private void PushSide() => _stateMachine.SetState<PushAbilitySideState>();

        private void PushUp()
        {
            _stateMachine.SetState<PushAbilityUPState>();
        }

        private void Attack() => _stateMachine.SetState<AttackState>();

        public void Tick()
        {
            float speed = Mathf.Abs(_player.Velocity.x);

            if (speed > 1f)
                _stateMachine.SetState<RunState>();
            else
                _stateMachine.SetState<IdleState>();
        }
    }
}