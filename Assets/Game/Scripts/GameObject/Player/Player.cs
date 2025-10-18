using Gameplay.Ability;
using UnityEngine;

namespace Gameplay
{
    public class Player : IInitializeble, IPushUpComponent, IPushSideComponent
    {
        private MoveComponent _moveComponent;
        private HealthComponent _healthComponent;
        private RotationComponent _rotationComponent;
        private PushAbility _ability;
        private SpriteAnimator _animator;
        private Transform _transform;

        [Inject]
        private void Construct(
            MoveComponent moveComponent,
            HealthComponent healthComponent,
            RotationComponent rotationComponent,
            PushAbility ability,
            SpriteAnimator animator,
            Transform transform)
        {
            _moveComponent = moveComponent;
            _healthComponent = healthComponent;
            _rotationComponent = rotationComponent;
            _ability = ability;
            _animator = animator;
            _transform = transform;
        }

        public void Initialize()
        {
            _moveComponent.AddCondition(_healthComponent.IsDead);
            _moveComponent.AddCondition(_animator.Lock);
            _rotationComponent.AddCondition(_healthComponent.IsDead);
        }

        void IPushUpComponent.Push()
        {
            _ability.Push(Vector2.up);
        }

        void IPushSideComponent.Push()
        {
            float x = _transform.localScale.x;
            _ability.Push(new Vector2(x, 0));
        }
    }
}