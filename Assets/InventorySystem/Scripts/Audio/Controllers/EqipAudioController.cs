using AudioEngine;
using Zenject;

namespace Inventories
{
    public class EqipAudioController : BaseAudioController<EqipItemAudioSignal>
    {
        public EqipAudioController(SignalBus signalBus, AudioEventKey key) : base(signalBus, key)
        {
        }
    }
}