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
        [SerializeField] private ItemDragHandler _itemDragHandler;

        public override void InstallBindings()
        {
            Container
                .Bind<ItemDragHandler>()
                .FromInstance(_itemDragHandler)
                .AsSingle();

            Container
                .BindInterfacesAndSelfTo<InventoryPresenter>()
                .AsSingle()
                .WithArguments(_inventorySize)
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<InventoryView>()
                .FromInstance(_inventoryUI)
                .AsSingle();

            Container
                .BindInterfacesAndSelfTo<InventoryAudioController>()
                .AsSingle()
                .NonLazy();

            var items = new List<ItemSettings>();

            foreach (var item in _initialItems)
                items.Add(item.itemSettings);

            Container
                .Bind<Inventory>()
                .FromMethod(() => new Inventory(_inventorySize.x, _inventorySize.y, items))
                .AsSingle();
        }
    }
}