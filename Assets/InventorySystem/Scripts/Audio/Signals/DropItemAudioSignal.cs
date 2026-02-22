using AudioEngine;

namespace Inventories
{
    public struct DropItemAudioSignal : IAudioSignal
    {
        public AudioEventKey AudioKey { get; set; }
    }
}