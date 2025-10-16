using Gameplay.Ability;
using UnityEngine;

namespace Gameplay
{
    public class Player : IInitializeble, IPushUpComponent, IPushSideComponent
    {
        [Inject] private MoveComponent moveComponent;
        [Inject] private HealthComponent healthComponent;
        [Inject] private RotationComponent rotationComponent;
        [Inject] private AttackComponent attackComponent;

        [Inject] private CollisionComponent collisionComponent;

        [Inject] private PushAbility ability;

        public void Initialize()
        {
            this.attackComponent.AddCondition(this.collisionComponent.IsGround);
            this.moveComponent.AddCondition(this.healthComponent.IsDead);
            this.rotationComponent.AddCondition(this.healthComponent.IsDead);
        }

        void IPushUpComponent.Push() => this.ability.Push(Vector2.up);

        void IPushSideComponent.Push() => this.ability.Push(Vector2.right);
    }
}