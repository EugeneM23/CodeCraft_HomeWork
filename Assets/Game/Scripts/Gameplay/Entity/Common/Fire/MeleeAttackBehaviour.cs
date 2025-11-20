using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public class MeleeAttackBehaviour : IEntityInit
    {
        int attack = Animator.StringToHash("Attack");
        private IAction _meleeAttackAction;
        private AnimationEventReceiver _animationEventReceiver;
        private Animator _animator;

        public void Init(in IEntity entity)
        {
            _animator = entity.GetAnimator();
            _meleeAttackAction = entity.GetMeleeAttackAction();
            _animationEventReceiver = entity.GetAnimationEventReceiver();
            _animationEventReceiver.OnEvent += Invoke;
        }

        private void Invoke(string eventName)
        {
            if (eventName == "melee_event")
            {
                _meleeAttackAction.Invoke();
            }
        }

        public bool CanMove()
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(1);

            return stateInfo.shortNameHash != attack;
        }
    }
}