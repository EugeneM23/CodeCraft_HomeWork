using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public sealed class CharacterAnimInstaller : MonoEntityInstaller
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationEventReceiver _animationReceiver;

        public override void Install(IEntity entity)
        {
            entity.AddAnimationEventReceiver(_animationReceiver);
            entity.AddAnimator(_animator);
            entity.AddBehaviour<CharacterMoveAnimBehaviour>();
            entity.AddBehaviour<SwitchAnimatorBehaviour>();
            entity.AddBehaviour<DeathAnimBehaviour>();
        }
    }
}