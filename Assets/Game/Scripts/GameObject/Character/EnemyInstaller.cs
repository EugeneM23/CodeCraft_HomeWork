using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class EnemyInstaller : Installer
    {
        [SerializeField] private Transform[] _waypoints;

        public override void Install(DiContainer container)
        {
            container.BindInterfacesAndSelf(new Enemy());
            container.BindInterfacesAndSelf(new EnemyBehaviour(_waypoints));
        }
    }
}