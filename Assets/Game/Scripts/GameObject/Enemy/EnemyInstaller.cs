using Gameplay;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class EnemyInstaller : Installer
    {
        [SerializeField] private CharacterController2D _character;
        [SerializeField] private int _health;

        public override void Install(DiContainer container)
        {
            container.BindSingle(_character);

            EnemyMoveController moveController = new EnemyMoveController();
            container.BindInterfacesAndSelf(moveController);
            container.BindInterfacesAndSelf(new Enemy(moveController));
            container.BindInterfacesAndSelf(new EnemyBehaviour());
            //container.BindInterfacesAndSelf(new AttackComponent());
            container.BindInterfacesAndSelf(new HealthComponent(_health));
            container.BindInterfacesAndSelf(new CharacterDeathObserver());
        }
    }
}