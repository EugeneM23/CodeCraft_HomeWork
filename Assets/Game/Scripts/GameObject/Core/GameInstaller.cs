using UnityEngine;

namespace Gameplay
{
    public class GameInstaller : Installer
    {
        [SerializeField] private TickableManager _tickableManager;

        public override void Install(DiContainer container)
        {
            container.Add(new InputReader());
            container.Add(_tickableManager);
        }
    }
}