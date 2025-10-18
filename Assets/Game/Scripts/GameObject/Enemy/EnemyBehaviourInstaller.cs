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
            container.BindSingle(new Sensor());
            container.BindSingle(new PatrolComponent(_patrolPoints));
            container.BindSingle(new EnemyAttackComponent(_targetLayer));
            container.BindSingle(new EnemyBehaviourController());
        }
    }
}