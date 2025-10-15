using UnityEngine;

namespace Gameplay
{
    public class Player : IInitializeble
    {
        private MoveComponent _moveComponent;
        private HealthComponent _healthComponent;
        private RotationComponent _rotationComponent;
        private RigidbodyForceComponent _rigidbodyForceComponent;
        private CooldownComponent _cooldownComponent;
        private AttackComponent _attackComponent;
        private CollisionComponent _collisionComponent;

        [Inject]
        private void Construct(
            MoveComponent moveComponent,
            HealthComponent healthComponent,
            RotationComponent rotationComponent,
            RigidbodyForceComponent rigidbodyForceComponent,
            CooldownComponent cooldownComponent,
            AttackComponent attackComponent,
            CollisionComponent collisionComponent
        )
        {
            _collisionComponent = collisionComponent;
            _attackComponent = attackComponent;
            _cooldownComponent = cooldownComponent;
            _moveComponent = moveComponent;
            _healthComponent = healthComponent;
            _rotationComponent = rotationComponent;
            _rigidbodyForceComponent = rigidbodyForceComponent;
        }

        public void Initialize()
        {
            _attackComponent.AddCondition(_collisionComponent.IsGround);
            _attackComponent.AddCondition(_cooldownComponent.IsNotRedy);
            _moveComponent.AddCondition(_healthComponent.IsDead);
            _moveComponent.AddCondition(_cooldownComponent.IsNotRedy);
            _rotationComponent.AddCondition(_healthComponent.IsDead);
            _rigidbodyForceComponent.AddCondition(_healthComponent.IsDead);
        }


        
    }
}