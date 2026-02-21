using System;
using AudioEngine;
using Zenject;

namespace Inventories
{
    public abstract class BaseAudioController<T> : IInitializable, IDisposable where T : IAudioSignal
    {
        private readonly SignalBus _signalBus;
        private AudioSystem _audioSystem;
        private readonly string _sound;

        private AudioEventKey Key;

        protected BaseAudioController(SignalBus signalBus, AudioEventKey key)
        {
            _signalBus = signalBus;
            Key = key;
        }

        public void Initialize()
        {
            _audioSystem = AudioSystem.Instance;
            _signalBus.Subscribe<T>(PlayeSound);
        }

        private void PlayeSound() =>
            _audioSystem.PlayEvent(Key);

        public void Dispose() =>
            _signalBus.Unsubscribe<T>(PlayeSound);
    }
}