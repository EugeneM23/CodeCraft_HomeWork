using Modules.PlayerController;

namespace Gameplay
{
    public abstract class BaseState
    {
        protected readonly SpriteAnimator _animator;
        protected readonly StateMachine _stateMachine;
        protected readonly PlayerController _player;

        protected BaseState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player)
        {
            _animator = animator;
            _stateMachine = stateMachine;
            _player = player;
        }

        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }

        public virtual void Tick()
        {
        }
    }
}