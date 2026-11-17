using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The animator of every body except the player's: Enemy, Zombie, Regax, Regax_Mount. The player
    /// has PlayerCharacterAnimInstaller.
    /// </summary>
    public sealed class CharacterAnimInstaller : MonoEntityInstaller
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
            entity.AddBehaviour<CharacterFootprintsBehaviour>();

            // AC_Zombie reaches Attack and TakeDamage only through AnyState transitions on those
            // triggers, and nothing used to set them, so neither animation could ever play. These
            // two are not the player's on purpose: AC_BaseCharacter declares no such parameters.
            entity.AddBehaviour<CharacterAttackAnimBehaviour>();
            entity.AddBehaviour<TakeDamageAnimBehaviour>();
        }
    }
}