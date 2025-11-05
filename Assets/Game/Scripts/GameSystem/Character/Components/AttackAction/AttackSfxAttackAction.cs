using AudioEngine;
using Gameplay;
using Gameplay.Controllers;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class AttackSfxAttackAction : AttackComponent.IAttackAction, IInitializeble
    {
        [Inject] private readonly Character _character;
        private float MAX_FRIQUIENCY = 0.1f;

        private AudioSystem _audioSystem;

        public void Initialize() => _audioSystem = AudioSystem.Instance;

        public void Invoke()
        {
            _audioSystem.PlayEvent(MasterBankAPI.SlashEvent, _character.Transfrom.position, Quaternion.identity,
                MAX_FRIQUIENCY);
        }
    }

    public class JumpSfxAttackAction : JumpComponent.IAction, IInitializeble
    {
        [Inject] private readonly Character _character;
        private float MAX_FRIQUIENCY = 0.1f;

        private AudioSystem _audioSystem;

        public void Initialize() => _audioSystem = AudioSystem.Instance;

        public void Invoke()
        {
            _audioSystem.PlayEvent(MasterBankAPI.SlashEvent, _character.Transfrom.position, Quaternion.identity,
                MAX_FRIQUIENCY);
        }
    }
}