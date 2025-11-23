using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class CharacterAnimInstaller : SceneEntityInstaller
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationEventReceiver _animationReceiver;
        [SerializeField] private IKHandController _IKController;

        public override void Install(IEntity entity)
        {
            entity.AddAnimationEventReceiver(_animationReceiver);
            entity.AddAnimator(_animator);
            entity.AddBehaviour<CharacterMoveAnimBehaviour>();
            entity.AddBehaviour<SwitchAnimatorBehaviour>();
            //entity.AddBehaviour<SwitchIKBehaviour>();

            entity.AddIKHandController(_IKController);
            entity.AddIsIKEnable(new ReactiveBool(true));
        }
    }
}