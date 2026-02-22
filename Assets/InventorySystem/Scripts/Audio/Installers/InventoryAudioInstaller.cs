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
            Container.DeclareSignal<EqipItemAudioSignal>();
            Container.DeclareSignal<DropItemAudioSignal>();

            Container.BindSignal<DropItemAudioSignal>()
                .ToMethod((signal) => AudioSystem.Instance.PlayEvent(signal.AudioKey));

            Container.BindSignal<EqipItemAudioSignal>()
                .ToMethod((signal) => AudioSystem.Instance.PlayEvent(signal.AudioKey));
        }
    }
}