using AudioEngine;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryAudioInstaller : MonoInstaller
    {
        [SerializeField] private AudioEventKey _dropToCellKey;
        [SerializeField] private AudioEventKey _equipKey;

        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<DropToCellAudioController>()
                .AsSingle()
                .WithArguments(_dropToCellKey)
                .NonLazy();

            Container
                .DeclareSignal<DropItemAudioSignal>();

            Container
                .BindInterfacesAndSelfTo<EqipAudioController>()
                .AsSingle()
                .WithArguments(_equipKey)
                .NonLazy();

            Container
                .DeclareSignal<EqipItemAudioSignal>();
        }
    }
}