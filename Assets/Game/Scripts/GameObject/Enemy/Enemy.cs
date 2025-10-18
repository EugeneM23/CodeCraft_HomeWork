using Game.Scripts.GameObject.Player;

namespace Gameplay
{
    public class Enemy : IInitializeble
    {
        private MoveComponent _moveComponent;
        private HealthComponent _healthComponent;
        private RotationComponent _rotationComponent;
        private SpriteAnimator _animator;
        private ImpulseComponent _impulseComponent;

        [Inject]
        private void Construct(
            MoveComponent moveComponent,
            HealthComponent healthComponent,
            RotationComponent rotationComponent,
            SpriteAnimator animator,
            ImpulseComponent impulseComponent)
        {
            _moveComponent = moveComponent;
            _healthComponent = healthComponent;
            _rotationComponent = rotationComponent;
            _animator = animator;
            _impulseComponent = impulseComponent;
        }

        public void Initialize()
        {
            _moveComponent.AddCondition(_healthComponent.IsDead);
            _moveComponent.AddCondition(_impulseComponent.OnImpulse);
            _moveComponent.AddCondition(_animator.Lock);
            _rotationComponent.AddCondition(_healthComponent.IsDead);
        }
    }
}