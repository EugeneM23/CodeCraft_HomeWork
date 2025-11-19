using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class CharacterAnimInstaller : SceneEntityInstaller
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationEventReceiver _animationReceiver;

        public override void Install(IEntity entity)
        {
            entity.AddAnimator(_animator);
            entity.AddBehaviour<CharacterMoveAnimBehaviour>();
            entity.AddBehaviour<SwitchAnimatorBehaviour>();
        }
    }
}