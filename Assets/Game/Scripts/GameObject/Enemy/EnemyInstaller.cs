using Gameplay;
using Gameplay.Controllers;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class EnemyInstaller : Installer
    {
        [SerializeField] private CharacterController2D _character;
        [SerializeField] private int _health;
        [SerializeField] private Transform[] _waypoints;
        [SerializeField] private LayerMask _damageLayer;
        [SerializeField] private Transform _prefab;

        public override void Install(DiContainer container)
        {
            container.BindSingle(_character);

            container.BindInterfacesAndSelf(new Enemy());
            container.BindInterfacesAndSelf(new EnemyBehaviour(_waypoints));
            container.BindInterfacesAndSelf(new HealthComponent(_health));
            container.BindInterfacesAndSelf(new CharacterDeathObserver());
            container.BindInterfacesAndSelf(new DamageCaster(_damageLayer, _prefab));
            container.BindInterfacesAndSelf(new AttackComponent());
        }
    }
}