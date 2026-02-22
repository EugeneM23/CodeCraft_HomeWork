using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryCoreInstaller : MonoInstaller
    {
        [SerializeField] private Vector2Int _inventorySize;
        [SerializeField] private InventoryView _inventoryUI;
        [SerializeField] private SceneItem[] _initialItems;

        public override void InstallBindings()
        {
            InstallInventoryCore();
            InventoryDragInstaller.Install(Container);
            InventorySignalsInstaller.Install(Container);
        }

        private void InstallInventoryCore()
        {
            var items = new List<ItemSettings>();
            foreach (var item in _initialItems)
                items.Add(item.itemSettings);

            Container
                .Bind<Inventory>()
                .FromMethod(() => new Inventory(_inventorySize.x, _inventorySize.y, items))
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
        }
    }
}