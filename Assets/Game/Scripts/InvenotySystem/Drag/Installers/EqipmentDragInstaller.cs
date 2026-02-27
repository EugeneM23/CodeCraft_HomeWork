using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Inventories
{
    [CreateAssetMenu(fileName = "EqipmentDragInstaller", menuName = "Inventories/EqipmentDragInstaller")]

    public class EqipmentDragInstaller : ScriptableObjectInstaller
    {
        public override void InstallBindings()
        {
            Container
                .Bind<DragChainProcessor>()
                .AsSingle();

            InstallBeginDragHandlers();
            InstallEndDragHandlers();

            // Регистрация коллекций хендлеров
            Container
                .Bind<List<IBeginDraghendler>>()
                .FromMethod(CreateBeginDragHandlers)
                .AsSingle();

            Container
                .Bind<List<IEndDraghendler>>()
                .FromMethod(CreateEndDragHandlers)
                .AsSingle();
        }

        private void InstallBeginDragHandlers()
        {
            Container.Bind<BeginDragFromEquipmentSlotHandler>().AsSingle();
        }

        private void InstallEndDragHandlers()
        {
            Container.Bind<DragConditionHandler>().AsSingle();
            Container.Bind<DropToEquipmentHandler>().AsSingle();
            Container.Bind<DropToInventoryHandler>().AsSingle();
            Container.Bind<ReturnItemToStartSlotHandler>().AsSingle();
        }

        private List<IBeginDraghendler> CreateBeginDragHandlers(InjectContext ctx)
        {
            return new List<IBeginDraghendler>
            {
                ctx.Container.Resolve<BeginDragFromEquipmentSlotHandler>(),
            };
        }

        private List<IEndDraghendler> CreateEndDragHandlers(InjectContext ctx)
        {
            return new List<IEndDraghendler>
            {
                ctx.Container.Resolve<DragConditionHandler>(),
                ctx.Container.Resolve<DropToEquipmentHandler>(),
                ctx.Container.Resolve<DropToInventoryHandler>(),
                ctx.Container.Resolve<ReturnItemToStartSlotHandler>(),
            };
        }
    }
}