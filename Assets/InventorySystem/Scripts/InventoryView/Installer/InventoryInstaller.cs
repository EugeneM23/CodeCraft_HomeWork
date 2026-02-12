using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryInstaller : MonoInstaller
    {
        [SerializeField] private Vector2Int _size;
        [SerializeField] private InventoryUI _inventoryUI;

        public override void InstallBindings()
        {
            Container
                .Bind<Inventory>()
                .FromInstance(new Inventory(_size.x, _size.y))
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