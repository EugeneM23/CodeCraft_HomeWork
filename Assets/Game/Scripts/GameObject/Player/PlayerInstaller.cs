using Gameplay.Controllers;
using Gameplay.Controllers.AttackAction;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class PlayerInstaller : Installer
    {
        [SerializeField] private CharacterController2D _character;
        [SerializeField] private int _health;
        [SerializeField] private Transform _attackEffect;

        public override void Install(DiContainer container)
        {
            Application.targetFrameRate = 140;

            container.BindSingle(_character);

            container.BindInterfacesAndSelf(new Player());
            container.BindInterfacesAndSelf(new AttackController());
            container.BindInterfacesAndSelf(new AttackComponent());
            container.BindInterfacesAndSelf(new HealthComponent(_health));
            container.BindInterfacesAndSelf(new CharacterDeathObserver());
            container.BindInterfacesAndSelf(new WallSmashDamageComponent());
            container.BindInterfacesAndSelf(new SpawnPlayerAction());

            container.BindInterfacesAndSelf(new SpawnEffectAction(_attackEffect));
        }
    }
}