using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject.VFX
{
    public class JumpVFXInstaller : Installer
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private SpriteAnimation[] _animations;

        public override void Install(DiContainer container)
        {
            var receiver = new AnimationEventReceiver();
            container.BindSingle(receiver);
            container.BindInterfacesAndSelf(new SpriteAnimator(_animations, _renderer, receiver));
        }
    }
}