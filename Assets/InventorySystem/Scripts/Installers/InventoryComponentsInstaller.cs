using Zenject;

namespace Inventories
{
    public class InventoryComponentsInstaller : Installer<InventoryComponentsInstaller>
    {
        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<CellHighlighter>()
                .AsSingle()
                .NonLazy();
        }
    }
}