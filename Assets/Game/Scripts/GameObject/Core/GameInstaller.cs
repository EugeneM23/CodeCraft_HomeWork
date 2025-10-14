using UnityEngine;

namespace Gameplay
{
    public class GameInstaller : Installer
    {
        public override void Install(DiContainer container)
        {
            container.BindSingle(new InputReader());
            container.BindSingle("Gameplay Installer");
        }
    }
}