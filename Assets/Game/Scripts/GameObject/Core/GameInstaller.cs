using UnityEngine;

namespace Gameplay
{
    public class GameInstaller : Installer
    {
        public override void Install(DiContainer container)
        {
            container.Add(GameID.InpuReader, new InputReader());
        }
    }
}