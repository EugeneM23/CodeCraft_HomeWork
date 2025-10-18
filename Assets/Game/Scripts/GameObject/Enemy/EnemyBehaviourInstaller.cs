using Gameplay.Ability;
using UnityEngine;

namespace Gameplay
{
    public class EnemyBehaviourInstaller : Installer
    {
        [SerializeField] private Transform[] _patrolPoints;
        [SerializeField] private LayerMask _targetLayer;

        public override void Install(DiContainer container)
        {
            container.BindSingle(new SensorComponent());
            container.BindSingle(new PatrolComponent(_patrolPoints));
            container.BindSingle(new EnemyAttackComponent());
            container.BindSingle(new EnemyBehaviourController());
        }
    }
}