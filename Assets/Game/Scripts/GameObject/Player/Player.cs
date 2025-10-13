using UnityEngine;

namespace Gameplay
{
    public class Player : IInitializeble
    {
        private MoveComponent _moveComponent;
        private HealthComponent _healthComponent;
        private RotationComponent _rotationComponent;
        private JumpComponent _jumpComponent;

        
        private void Construct(
            MoveComponent moveComponent,
            HealthComponent healthComponent,
            RotationComponent rotationComponent,
            JumpComponent jumpComponent
        )
        {
            _moveComponent = moveComponent;
            _healthComponent = healthComponent;
            _rotationComponent = rotationComponent;
            _jumpComponent = jumpComponent;
        }

        public void Initialize()
        {
            _moveComponent.AddCondition(_healthComponent.IsDead);
            _rotationComponent.AddCondition(_healthComponent.IsDead);
            _jumpComponent.AddCondition(_healthComponent.IsDead);
        }
    }
}