using AudioEngine;
using Gameplay;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class DeathSfxAction : CharacterDeathObserver.IAction, IInitializeble
    {
        [Inject] private readonly Character _character;
        private float MAX_FRIQUIENCY = 0.1f;

        private AudioSystem _audioSystem;

        public void Initialize() => _audioSystem = AudioSystem.Instance;

        public void Invoke(CharacterController2D character)
        {
            _audioSystem.PlayEvent(MasterBankAPI.DeathEvent, _character.Transfrom.position, Quaternion.identity,
                MAX_FRIQUIENCY);
        }
    }
}