using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class BaseState
    {
        protected SpriteAnimator _animator;
        protected AnimationFSM AnimationFsm;
        protected Character _character;

        [Inject]
        public void Construct(SpriteAnimator animator, AnimationFSM animationFsm, Character character)
        {
            _character = character;
            _animator = animator;
            AnimationFsm = animationFsm;
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