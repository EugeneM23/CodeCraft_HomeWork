using System;
using AudioEngine;
using Gameplay;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class StepSFXComponent : IInitializeble
    {
        [Inject] private readonly Character _character;
        [Inject] private readonly SpriteAnimator _animator;

        private AudioSystem _audioSystem;

        public void Initialize()
        {
            _audioSystem = AudioSystem.Instance;
            _animator.OnEventRaised += PlaySound;
        }

        private void PlaySound(EventID id)
        {
            if (id == EventID.Step) 
                PlayStep();
        }

        public void PlayStep()
        {
            _audioSystem.PlayEvent(MasterBankAPI.StepEvent, _character.Transfrom.position, Quaternion.identity);
        }
    }
}