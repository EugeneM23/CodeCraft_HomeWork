using UnityEngine;
using Zenject;

namespace Equipment
{
    public class CharacterEquipmentInstaller : MonoInstaller
    {
        [SerializeField] private CharacterEquipment _characterEquipment;

        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<EquipmentModel>()
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<CharacterEquipment>()
                .FromInstance(_characterEquipment)
                .AsSingle()
                .NonLazy();
        }
    }
}