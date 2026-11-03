using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class EnemyAnimInstaller : MonoEntityInstaller
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

            // AC_Zombie reaches Attack and TakeDamage only through AnyState transitions on those
            // triggers, and nothing used to set them, so neither animation could ever play. These
            // two are enemy-only on purpose: AC_BaseCharacter declares no such parameters.
            entity.AddBehaviour<EnemyAttackAnimBehaviour>();
            entity.AddBehaviour<EnemyTakeDamageAnimBehaviour>();
        }
    }
}