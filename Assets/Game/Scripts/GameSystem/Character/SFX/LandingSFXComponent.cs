using AudioEngine;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class LandingSFXComponent : IInitializeble
    {
        [Inject] private readonly Character _character;

        private AudioSystem _audioSystem;

        public void Initialize()
        {
            _audioSystem = AudioSystem.Instance;
            _character.OnGrounded += PlayLanding;
        }

        public void PlayLanding(Vector2 position)
        {
            _audioSystem.PlayEvent(MasterBankAPI.LandingEvent, _character.Transfrom.position, Quaternion.identity);
        }
    }
}