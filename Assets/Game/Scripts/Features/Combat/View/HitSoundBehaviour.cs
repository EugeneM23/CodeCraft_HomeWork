using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class HitSoundBehaviour : IEntityInit, IEntityDispose
    {
        private readonly AudioClip[] _damageClips;
        private readonly AudioSource _audioSource;
        private Health _health;

        public HitSoundBehaviour(AudioClip[] damageClips, AudioSource audioSource)
        {
            _damageClips = damageClips;
            _audioSource = audioSource;
        }

        public void Init(IEntity entity)
        {
            _health = entity.GetHealth();
            _health.OnHealthChanged += PlayHitSound;
        }

        public void Dispose(IEntity entity)
        {
            // Without this the handler outlived the entity on a Health instance that the pool
            // hands back out, so a reused body played the previous owner sounds as well.
            if (_health != null)
                _health.OnHealthChanged -= PlayHitSound;

            _health = null;
        }

        private void PlayHitSound(int health)
        {
            if (_audioSource == null || _damageClips == null || _damageClips.Length == 0)
                return;

            int range = Random.Range(0, _damageClips.Length);
            _audioSource.PlayOneShot(_damageClips[range]);
        }
    }
}
