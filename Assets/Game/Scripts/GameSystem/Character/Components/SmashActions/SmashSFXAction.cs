using AudioEngine;
using Gameplay.Controllers;
using UnityEngine;

namespace Gameplay
{
    public class SmashSFXAction : SmashComponent.IAction, ITickable, IInitializeble
    {
        [Inject] private Character _controller;

        private bool _effectSpawned = true;
        private AudioSystem _audioSystem;

        public void Initialize() => _audioSystem = AudioSystem.Instance;

        public void Invoke()
        {
            _effectSpawned = false;
        }

        public void Tick()
        {
            if (!_effectSpawned && _controller.IsGrounded)
            {
                _audioSystem.PlayEvent(MasterBankAPI.SmashEvent, _controller.Transfrom.position, Quaternion.identity);
                _effectSpawned = true;
            }
        }
    }
}