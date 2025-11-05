using AudioEngine;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class StepSFXComponent : IInitializeble
    {
        [Inject] private readonly Character _character;

        private AudioSystem _audioSystem;

        public void Initialize() => _audioSystem = AudioSystem.Instance;

        public void PlayStep()
        {
            _audioSystem.PlayEvent(MasterBankAPI.StepEvent, _character.Transfrom.position, Quaternion.identity);
        }
    }
}