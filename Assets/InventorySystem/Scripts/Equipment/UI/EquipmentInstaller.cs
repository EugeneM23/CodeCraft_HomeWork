using UnityEngine;
using Zenject;

namespace Equipment
{
    public class EquipmentInstaller : MonoInstaller
    {
        [SerializeField] private EquipmentView _equipmentView;
        [SerializeField] private EquipmentSlot[] _slots;

        public override void InstallBindings()
        {
            Container
                .Bind<EquipmentView>()
                .FromInstance(_equipmentView)
                .AsSingle();

            Container
                .Bind<EquipmentSlot[]>()
                .FromInstance(_slots)
                .AsSingle();

            Container
                .BindInterfacesAndSelfTo<EquipmentToggleController>()
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<EquipmentSlotAudioController>()
                .AsSingle()
                .NonLazy();
        }
    }
}