using Game.Scripts.GameSystem.Controllers.Player.StateMachine;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.Modules.SpriteAnimator
{
    public class AnimationController : ITickable
    {
        private SpriteAnimator _animator;
        private StateMachine _stateMachine;

        [Inject]
        public void Construct(SpriteAnimator animator, StateMachine stateMachine)
        {
            _animator = animator;
            _stateMachine = stateMachine;
        }

        public void Tick()
        {
            if (_stateMachine.CurrentState.GetType() == typeof(IdleState)) _animator.Play(AnimationName.Idle);
            if (_stateMachine.CurrentState.GetType() == typeof(RunState)) _animator.Play(AnimationName.Run);
            if (_stateMachine.CurrentState.GetType() == typeof(FallState)) _animator.Play(AnimationName.Fall);
            if (_stateMachine.CurrentState.GetType() == typeof(AttackState)) _animator.Play(AnimationName.Attack);        }
    }
}