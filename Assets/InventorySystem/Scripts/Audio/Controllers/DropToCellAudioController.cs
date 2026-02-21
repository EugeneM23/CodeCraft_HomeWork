using AudioEngine;
using Zenject;

namespace Inventories
{
    public class DropToCellAudioController : BaseAudioController<DropItemAudioSignal>
    {
        public DropToCellAudioController(SignalBus signalBus, AudioEventKey key) : base(signalBus, key)
        {
        }
    }
}