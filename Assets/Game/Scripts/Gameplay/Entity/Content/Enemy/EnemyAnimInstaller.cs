using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class EnemyAnimInstaller : SceneEntityInstaller
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationEventReceiver _animationReceiver;

        public override void Install(IEntity entity)
        {
            entity.AddAnimationEventReceiver(_animationReceiver);
            entity.AddAnimator(_animator);
            entity.AddBehaviour<SwitchAnimatorBehaviour>();
            entity.AddBehaviour<CharacterMoveAnimBehaviour>();
            entity.AddBehaviour<DeathAnimBehaviour>();
        }
    }
}