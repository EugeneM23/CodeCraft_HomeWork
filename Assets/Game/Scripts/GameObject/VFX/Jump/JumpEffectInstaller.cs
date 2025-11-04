using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject.VFX.Jump
{
    public class JumpEffectInstaller : Installer
    {
        [SerializeField]private SpriteAnimation[] _animations;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        public override void Install(DiContainer container)
        {
            var spriteAnimator = new SpriteAnimator(_animations, _spriteRenderer);
            container.BindInterfacesAndSelf(spriteAnimator);

        }
    }
}