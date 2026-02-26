using UnityEngine;
using Zenject;

public class CameraInstaller : MonoInstaller
{
    [SerializeField] private CameraMovement _cameraMovement;
    [SerializeField] private InventoryDebug _inventoryDebug;

    public override void InstallBindings()
    {
        Container
            .Bind<CameraMovement>()
            .FromInstance(_cameraMovement)
            .AsSingle();

        Container
            .Bind<InventoryDebug>()
            .FromInstance(_inventoryDebug)
            .AsSingle();

        Container
            .BindInterfacesAndSelfTo<CameraController>()
            .AsSingle()
            .NonLazy();
    }
}