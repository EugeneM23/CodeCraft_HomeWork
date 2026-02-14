using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryInstaller : MonoInstaller
    {
        [SerializeField] private Vector2Int _size;
        [SerializeField] private InventoryUI _inventoryUI;
        [SerializeField] private SceneItem[] _initialItems;

        public override void InstallBindings()
        {
            List<ItemData> items = new();
            foreach (var initialItem in _initialItems) 
                items.Add(initialItem.ItemData);

            Container
                .Bind<Inventory>()
                .FromInstance(new Inventory(_size.x, _size.y, items))
                .AsSingle()
                .NonLazy();

            Container
                .Bind<InventoryAdapter>()
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<InventoryUI>()
                .FromInstance(_inventoryUI)
                .AsSingle()
                .NonLazy();
        }
    }
}