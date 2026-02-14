using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryInstaller : MonoInstaller
    {
        [SerializeField] private Vector2Int _inventorySize;
        [SerializeField] private Vector2Int _cellSize;
        [SerializeField] private RectTransform _inventoryItemPrefab;
        [SerializeField] private CellView _cellPrefab;
        [SerializeField] private RectTransform _gridContainer;
        [SerializeField] private InventoryUI _inventoryUI;
        [SerializeField] private SceneItem[] _initialItems;

        public override void InstallBindings()
        {
            var items = new List<ItemData>();
            foreach (var item in _initialItems)
                items.Add(item.ItemData);

            var inventory = new Inventory(_inventorySize.x, _inventorySize.y, items);

            Container.Bind<Inventory>().FromInstance(inventory).AsSingle();
            
            Container.BindInterfacesAndSelfTo<InventoryAdapter>().AsSingle()
                .WithArguments(_cellSize, _inventoryItemPrefab, _cellPrefab, _gridContainer);
            
            Container.Bind<InventoryUI>().FromInstance(_inventoryUI).AsSingle();
            
            Container.BindInterfacesTo<DragItemController>().AsSingle();
            
            // Установка SignalBus и объявление локального сигнала
            SignalBusInstaller.Install(Container);
            Container.DeclareSignal<ItemRemovedSignal>();
        }
    }

    public class ItemRemovedSignal
    {
        public InventoryItem Item { get; }

        public ItemRemovedSignal(InventoryItem item)
        {
            Item = item;
        }
    }
}