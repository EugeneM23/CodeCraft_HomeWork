using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public class HitSoundBehaviour : IEntityInit
    {
        private readonly AudioClip[] _damageClips;
        private readonly AudioSource _audioSource;
        private Health _health;

        public HitSoundBehaviour(AudioClip[] damageClips, AudioSource audioSource)
        {
            _damageClips = damageClips;
            _audioSource = audioSource;
        }

        public void Init(in IEntity entity)
        {
            _health = entity.GetHealth();
            _health.OnHealthChanged += PlayHitSound;
        }

        private void PlayHitSound(int obj)
        {
            int range = Random.Range(0, _damageClips.Length);
            _audioSource.PlayOneShot(_damageClips[range]);
        }
    }
}