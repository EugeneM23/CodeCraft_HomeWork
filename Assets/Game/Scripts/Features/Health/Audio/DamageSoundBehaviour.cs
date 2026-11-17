using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Plays a random clip from the list on every hit.
    /// </summary>
    /// <remarks>
    /// Replaces two near-copies: HitSoundBehaviour (enemies) listened to Health.OnHealthChanged,
    /// which also fires on a heal, so healing sounded like being hit; TakeDamageSoundBehaviour (the
    /// player) listened to DamageTakenEvent with a single clip. Both now hear the hit itself, the
    /// killing blow included.
    /// </remarks>
    public sealed class DamageSoundBehaviour : IEntityInit, IEntityDispose
    {
        private readonly AudioClip[] _clips;
        private readonly AudioSource _audioSource;

        private IEvent<TakeDamageArgs> _damageEvent;

        public DamageSoundBehaviour(AudioClip[] clips, AudioSource audioSource)
        {
            _clips = clips;
            _audioSource = audioSource;
        }

        public void Init(IEntity entity)
        {
            _damageEvent = entity.GetDamageTakenEvent();
            _damageEvent.Subscribe(this.OnDamageTaken);
        }

        public void Dispose(IEntity entity) => _damageEvent?.Unsubscribe(this.OnDamageTaken);

        private void OnDamageTaken(TakeDamageArgs _)
        {
            if (_audioSource == null || _clips == null || _clips.Length == 0)
                return;

            AudioClip clip = _clips[Random.Range(0, _clips.Length)];

            if (clip != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}
