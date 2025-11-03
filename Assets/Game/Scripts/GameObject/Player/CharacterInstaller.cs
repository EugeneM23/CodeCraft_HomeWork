using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class CharacterInstaller : Installer
    {
        [SerializeField] private CharacterController2D _character;
        [SerializeField] private int _health;

        public override void Install(DiContainer container)
        {
            Application.targetFrameRate = 140;

            container.BindSingle(_character);
            container.BindInterfacesAndSelf(new HealthComponent(_health));
            container.BindInterfacesAndSelf(new CharacterDeathObserver());
            container.BindInterfacesAndSelf(new WallSmashDamageComponent());
            container.BindInterfacesAndSelf(new SpawnPlayerAction());
        }
    }
}