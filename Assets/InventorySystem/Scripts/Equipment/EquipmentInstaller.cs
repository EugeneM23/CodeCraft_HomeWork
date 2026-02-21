using UnityEngine;
using Zenject;

public class EquipmentInstaller : MonoInstaller
{
    [SerializeField] private EquipmentView _equipmentView;

    public override void InstallBindings()
    {
        Container
            .Bind<EquipmentView>()
            .FromInstance(_equipmentView)
            .AsSingle();

        Container
            .BindInterfacesAndSelfTo<EquipmentPresenter>()
            .AsSingle()
            .NonLazy();
    }
}