using UnityEngine;

namespace Gameplay
{
    public class Player : IInitializeble
    {
        private MoveComponent _moveComponent;
        private HealthComponent _healthComponent;
        private RotationComponent _rotationComponent;
        private JumpComponent _jumpComponent;

        public void Initialize()
        {
            _healthComponent = ServiceLocator.Get<HealthComponent>(PlayerId.HealthComponent);
            _rotationComponent = ServiceLocator.Get<RotationComponent>(PlayerId.RotationComponent);
            _moveComponent = ServiceLocator.Get<MoveComponent>(PlayerId.MoveComponent);
            _jumpComponent = ServiceLocator.Get<JumpComponent>(PlayerId.JumpComponent);


            _moveComponent.AddCondition(_healthComponent.IsDead);
            _rotationComponent.AddCondition(_healthComponent.IsDead);
            _jumpComponent.AddCondition(_healthComponent.IsDead);
        }
    }
}