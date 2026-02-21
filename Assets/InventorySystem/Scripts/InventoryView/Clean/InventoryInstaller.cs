using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryInstaller : MonoInstaller
    {
        [SerializeField] private Vector2Int _inventorySize;
        [SerializeField] private InventoryView _inventoryUI;
        [SerializeField] private SceneItem[] _initialItems;

        public override void InstallBindings()
        {
            var items = new List<ItemData>();
            foreach (var item in _initialItems)
                items.Add(item.ItemData);

            var inventory = new Inventory(_inventorySize.x, _inventorySize.y, items);

            SignalBusInstaller.Install(Container);

            Container
                .DeclareSignal<OpenEquipmentSignal>();

            Container
                .Bind<Inventory>()
                .FromInstance(inventory)
                .AsSingle();

            Container
                .BindInterfacesAndSelfTo<InventoryPresenter>()
                .AsSingle()
                .WithArguments(_inventorySize)
                .NonLazy();

            Container
                .Bind<InventoryView>()
                .FromInstance(_inventoryUI)
                .AsSingle();

            Container
                .Bind<DragContext>()
                .AsSingle();

            Container
                .BindInterfacesAndSelfTo<CellHighlighter>()
                .AsSingle()
                .NonLazy();
        }
    }
}