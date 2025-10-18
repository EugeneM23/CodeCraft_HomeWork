using Game.Scripts.GameObject.Player;

namespace Gameplay
{
    public class Enemy : IInitializeble
    {
        [Inject] private MoveComponent moveComponent;
        [Inject] private HealthComponent healthComponent;
        [Inject] private RotationComponent rotationComponent;
        [Inject] private SpriteAnimator animator;
        [Inject] private ImpulseComponent impulseComponent;

        public void Initialize()
        {
            this.moveComponent.AddCondition(this.healthComponent.IsDead);
            this.moveComponent.AddCondition(this.impulseComponent.OnImpulse);
            this.moveComponent.AddCondition(animator.Lock);
            this.rotationComponent.AddCondition(this.healthComponent.IsDead);
        }
    }
}