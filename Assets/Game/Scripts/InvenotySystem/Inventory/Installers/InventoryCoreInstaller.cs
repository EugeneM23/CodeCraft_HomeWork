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