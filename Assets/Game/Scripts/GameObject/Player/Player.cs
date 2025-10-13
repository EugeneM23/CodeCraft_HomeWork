namespace Gameplay
{
    public class Player : IInitializeble
    {
        private MoveComponent _moveComponent;
        private HealthComponent _healthComponent;
        private RotationComponent _rotationComponent;
        private JumpComponent _jumpComponent;
        private AttackCooldown _cooldown;
        private AttackComponent _attackComponent;
        private CollisionComponent _collisionComponent;

        [Inject]
        private void Construct(
            MoveComponent moveComponent,
            HealthComponent healthComponent,
            RotationComponent rotationComponent,
            JumpComponent jumpComponent,
            AttackCooldown cooldown,
            AttackComponent attackComponent,
            CollisionComponent collisionComponent
        )
        {
            _collisionComponent = collisionComponent;
            _attackComponent = attackComponent;
            _cooldown = cooldown;
            _moveComponent = moveComponent;
            _healthComponent = healthComponent;
            _rotationComponent = rotationComponent;
            _jumpComponent = jumpComponent;
        }

        public void Initialize()
        {
            
            _attackComponent.AddCondition(_collisionComponent.IsGround);
            _attackComponent.AddCondition(_cooldown.CanAttack);
            _moveComponent.AddCondition(_healthComponent.IsDead);
            _moveComponent.AddCondition(_cooldown.CanAttack);
            _rotationComponent.AddCondition(_healthComponent.IsDead);
            _jumpComponent.AddCondition(_healthComponent.IsDead);
        }
    }
}