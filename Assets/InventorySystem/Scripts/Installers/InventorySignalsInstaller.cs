using Zenject;

namespace Inventories
{
    public class InventorySignalsInstaller : Installer<InventorySignalsInstaller>
    {
        public override void InstallBindings()
        {
            SignalBusInstaller.Install(Container);

            Container.DeclareSignal<OpenEquipmentSignal>();
        }
    }
}