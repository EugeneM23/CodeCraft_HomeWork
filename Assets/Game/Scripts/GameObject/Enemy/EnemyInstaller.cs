using Gameplay;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class EnemyInstaller : Installer
    {
        [SerializeField] private CharacterController2D _character;
        [SerializeField] private int _health;
        [SerializeField] private Transform[] _waypoints;

        public override void Install(DiContainer container)
        {
            container.BindSingle(_character);

            container.BindInterfacesAndSelf(new Enemy());
            container.BindInterfacesAndSelf(new EnemyBehaviour(_waypoints));
            container.BindInterfacesAndSelf(new HealthComponent(_health));
            container.BindInterfacesAndSelf(new CharacterDeathObserver());
        }
    }
}