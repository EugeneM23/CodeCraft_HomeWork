using System;
using Gameplay.Controllers;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class PlayerInstaller : Installer
    {
        public override void Install(DiContainer container)
        {
            Application.targetFrameRate = 140;

            container.BindInterfacesAndSelf(new Player());
            container.BindInterfacesAndSelf(new MoveController());
            container.BindInterfacesAndSelf(new JumpController());
            container.BindInterfacesAndSelf(new SmashController());
            container.BindInterfacesAndSelf(new DashController());
            container.BindInterfacesAndSelf(new SpawnPlayerAction());
            container.BindInterfacesAndSelf(new AttackController());
            container.BindInterfacesAndSelf(new ThrowItemController());
            container.BindInterfacesAndSelf(new WallSmashDamageComponent());
        }
    }
}