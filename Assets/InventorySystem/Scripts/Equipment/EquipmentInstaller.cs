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
            // View
            Container
                .Bind<EquipmentView>()
                .FromInstance(_equipmentView)
                .AsSingle()
                .NonLazy();

            // Presenter
            Container
                .BindInterfacesAndSelfTo<EquipmentPresenter>()
                .AsSingle()
                .NonLazy();

            // Controllers
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