using Equipment.Equipment;
using UnityEngine;
using Zenject;

namespace Equipment
{
    public class EquipmentInstaller : MonoInstaller
    {
        [SerializeField] private EquipmentView _equipmentView;
        [SerializeField] private CharacterEquipment _characterEquipment;

        public override void InstallBindings()
        {
            Container
                .Bind<EquipmentView>()
                .FromInstance(_equipmentView)
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<EquipmentPresenter>()
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<EquipmentSlotAudioController>()
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<CharacterEquipmentController>()
                .AsSingle()
                .NonLazy();
        }
    }
}