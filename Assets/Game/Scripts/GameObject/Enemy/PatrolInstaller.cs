using UnityEngine;

namespace Gameplay
{
    public class PatrolInstaller : Installer
    {
        [SerializeField] private Transform[] _patrolPoints;

        public override void Install(DiContainer container)
        {
            container.BindSingle(new PatrolComponent(_patrolPoints));
        }
    }
}