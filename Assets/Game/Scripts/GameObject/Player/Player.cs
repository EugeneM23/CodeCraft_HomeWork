using Gameplay.Ability;
using UnityEngine;

namespace Gameplay
{
    public class Player : IInitializeble, IPushUpComponent, IPushSideComponent
    {
        [Inject] private MoveComponent moveComponent;
        [Inject] private HealthComponent healthComponent;
        [Inject] private RotationComponent rotationComponent;
        [Inject] private PushAbility ability;
        [Inject] private SpriteAnimator animator;
        [Inject] private Transform transform;

        public void Initialize()
        {
            this.moveComponent.AddCondition(this.healthComponent.IsDead);
            this.moveComponent.AddCondition(animator.Lock);
            this.rotationComponent.AddCondition(this.healthComponent.IsDead);
        }

        void IPushUpComponent.Push()
        {
            this.ability.Push(Vector2.up);
        }

        void IPushSideComponent.Push()
        {
            float x = this.transform.localScale.x;
            this.ability.Push(new Vector2(x, 0));
        }
    }
}