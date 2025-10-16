using Gameplay;
using UnityEngine;

namespace Game.Scripts.Modules.SpriteAnimator
{
    public class SpriteAnimatorInstaller : Installer
    {
        [SerializeField] private SpriteAnimation[] _animation;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        public override void Install(DiContainer container)
        {
            container.BindSingle(new SpriteAnimator(_animation, _spriteRenderer));
            container.BindSingle(new AnimationController());
        }
    }
}