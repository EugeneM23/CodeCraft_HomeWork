using UnityEngine;

namespace Gameplay
{
    public class GameInstaller : Installer
    {
        public override void Install(DiContainer container)
        {
            container.Add(new InputReader());
            container.Add("Gameplay Installer");
        }
    }
}