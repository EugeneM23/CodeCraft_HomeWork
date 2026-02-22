using AudioEngine;

namespace Inventories
{
    public struct EqipItemAudioSignal : IAudioSignal
    {
        public AudioEventKey AudioKey { get; set; }
    }
}