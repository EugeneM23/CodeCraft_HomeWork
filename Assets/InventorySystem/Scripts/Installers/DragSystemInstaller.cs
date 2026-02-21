using System.Collections.Generic;
using Zenject;

namespace Inventories
{
    public class DragSystemInstaller : Installer<DragSystemInstaller>
    {
        public override void InstallBindings()
        {
            // Регистрация DragContext
            Container
                .Bind<DragContext>()
                .AsSingle();

            // Регистрация процессора драга
            Container
                .Bind<DragChainProcessor>()
                .AsSingle();

            // Регистрация всех хендлеров
            InstallBeginDragHandlers();
            InstallEndDragHandlers();

            // Регистрация коллекций хендлеров
            Container
                .Bind<List<IDragChainHandler>>()
                .WithId("BeginDragHandlers")
                .FromMethod(CreateBeginDragHandlers)
                .AsCached();

            Container
                .Bind<List<IDragChainHandler>>()
                .WithId("EndDragHandlers")
                .FromMethod(CreateEndDragHandlers)
                .AsCached();
        }

        private void InstallBeginDragHandlers()
        {
            Container.Bind<BeginDragFromEquipmentSlotHandler>().AsSingle();
            Container.Bind<BeginDragFromInventoryCellHandler>().AsSingle();
        }

        private void InstallEndDragHandlers()
        {
            Container.Bind<DragConditionHandler>().AsSingle();
            Container.Bind<DropToEquipmentHandler>().AsSingle();
            Container.Bind<DropToInventoryHandler>().AsSingle();
            Container.Bind<ReturnItemToStartPositionHandler>().AsSingle();
        }

        private List<IDragChainHandler> CreateBeginDragHandlers(InjectContext ctx)
        {
            return new List<IDragChainHandler>
            {
                ctx.Container.Resolve<BeginDragFromEquipmentSlotHandler>(),
                ctx.Container.Resolve<BeginDragFromInventoryCellHandler>(),
            };
        }

        private List<IDragChainHandler> CreateEndDragHandlers(InjectContext ctx)
        {
            return new List<IDragChainHandler>
            {
                ctx.Container.Resolve<DragConditionHandler>(),
                ctx.Container.Resolve<DropToEquipmentHandler>(),
                ctx.Container.Resolve<DropToInventoryHandler>(),
                ctx.Container.Resolve<ReturnItemToStartPositionHandler>()
            };
        }
    }
}