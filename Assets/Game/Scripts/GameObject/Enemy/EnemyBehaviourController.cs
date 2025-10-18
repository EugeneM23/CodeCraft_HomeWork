namespace Gameplay
{
    public class EnemyBehaviourController : IInitializeble, ITickable
    {
        private PatrolComponent _patrol;
        private EnemyAttackComponent _attackComponent;

        [Inject]
        private void Construct(PatrolComponent patrol, EnemyAttackComponent attackComponent)
        {
            _patrol = patrol;
            _attackComponent = attackComponent;
        }

        public void Initialize()
        {
            _patrol.Initialize();
            _attackComponent.Initialize();
        }

        public void Tick()
        {
            _attackComponent.Tick();

            if (_attackComponent.HasTarget)
            {
                _patrol.IsActive = false;
            }
            else
            {
                _patrol.IsActive = true;
                _patrol.Tick();
            }
        }
    }
}