using System.Collections.Generic;
using NUnit.Framework.Internal;
using UnityEngine;
using Zenject;

namespace Inventories
{
    [CreateAssetMenu(fileName = "InventoryDragInstaller", menuName = "Inventories/InventoryDragInstaller")]
    public class InventoryDragInstaller : ScriptableObjectInstaller
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
            Container.Bind<BeginDragFromInventoryCellHandler>().AsSingle();
        }

        private void InstallEndDragHandlers()
        {
            Container.Bind<DragConditionHandler>().AsSingle();
            Container.Bind<DropToInventoryHandler>().AsSingle();
            Container.Bind<ReturnItemToStartCellHandler>().AsSingle();
            
            Container.Bind<ReturnItemToStartSlotHandler>().AsSingle();
            Container.Bind<DropToEquipmentHandler>().AsSingle();

        }

        private List<IBeginDraghendler> CreateBeginDragHandlers(InjectContext ctx)
        {
            return new List<IBeginDraghendler>
            {
                ctx.Container.Resolve<BeginDragFromInventoryCellHandler>(),
            };
        }

        private List<IEndDraghendler> CreateEndDragHandlers(InjectContext ctx)
        {
            return new List<IEndDraghendler>
            {
                ctx.Container.Resolve<DragConditionHandler>(),
                ctx.Container.Resolve<DropToInventoryHandler>(),
                
                ctx.Container.Resolve<DropToEquipmentHandler>(),
                
                ctx.Container.Resolve<ReturnItemToStartSlotHandler>(),
                ctx.Container.Resolve<ReturnItemToStartCellHandler>(),
            };
        }
    }
}