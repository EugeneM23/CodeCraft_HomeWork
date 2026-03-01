using UnityEngine;
using Zenject;

public class CameraInstaller : MonoInstaller
{
    [SerializeField] private CameraMovement _cameraMovement;
    [SerializeField] private GameScreen gameScreen;

    public override void InstallBindings()
    {
        Container
            .Bind<CameraMovement>()
            .FromInstance(_cameraMovement)
            .AsSingle();

        Container
            .Bind<GameScreen>()
            .FromInstance(gameScreen)
            .AsSingle();

        Container
            .BindInterfacesAndSelfTo<CameraController>()
            .AsSingle()
            .NonLazy();
    }
}