using Inventories;
using UnityEngine;
using Zenject;

namespace Equipment
{
    public class EquipmentInstaller : MonoInstaller
    {
        [SerializeField] private EquipmentView _equipmentView;
        [SerializeField] private ItemDragHandler _itemDragHandler;

        public override void InstallBindings()
        {
            Container
                .Bind<ItemDragHandler>()
                .FromInstance(_itemDragHandler);

            Container
                .Bind<EquipmentView>()
                .FromInstance(_equipmentView)
                .AsSingle()
                .NonLazy();

            Container
                .Bind<EquipmentModel>()
                .AsSingle();

            Container
                .BindInterfacesAndSelfTo<EquipmentPresenter>()
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<EquipmentAudioController>()
                .AsSingle()
                .NonLazy();
        }
    }
}