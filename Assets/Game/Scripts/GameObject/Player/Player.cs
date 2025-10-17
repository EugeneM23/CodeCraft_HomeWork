using Game.Scripts.Modules.SpriteAnimator;
using Gameplay.Ability;
using UnityEngine;

namespace Gameplay
{
    public class Player : IInitializeble, IPushUpComponent, IPushSideComponent
    {
        [Inject] private MoveComponent moveComponent;
        [Inject] private HealthComponent healthComponent;
        [Inject] private RotationComponent rotationComponent;
        [Inject] private CollisionComponent collisionComponent;
        [Inject] private PushAbility ability;
        [Inject] private SpriteAnimator animator;

        public void Initialize()
        {
            this.moveComponent.AddCondition(this.healthComponent.IsDead);
            this.moveComponent.AddCondition(animator.Lock);
            this.rotationComponent.AddCondition(this.healthComponent.IsDead);
        }

        void IPushUpComponent.Push() => this.ability.Push(Vector2.up);

        void IPushSideComponent.Push() => this.ability.Push(Vector2.right);
    }
}