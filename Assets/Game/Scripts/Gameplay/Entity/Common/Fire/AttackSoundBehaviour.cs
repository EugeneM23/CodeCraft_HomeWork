using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class AttackSoundBehaviour : IEntityInit
    {
        private readonly AudioClip[] _audionClips;
        private readonly AudioSource _audioSource;
        private BaseEvent _fireEvent;

        public AttackSoundBehaviour(AudioClip[] attaClips, AudioSource audioSource)
        {
            _audionClips = attaClips;
            _audioSource = audioSource;
        }

        public void Init(in IEntity entity)
        {
            _fireEvent = entity.GetFireEvent();
            _fireEvent.Subscribe(PlaySound);
        }

        private void PlaySound()
        {
            int range = Random.Range(0, _audionClips.Length);
            _audioSource.PlayOneShot(_audionClips[range]);
        }
    }
}