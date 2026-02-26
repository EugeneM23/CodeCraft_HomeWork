using Zenject;

namespace Inventories
{
    public class InventoryAudioInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<InventoryAudioController>()
                .AsSingle()
                .NonLazy();
        }
    }
}