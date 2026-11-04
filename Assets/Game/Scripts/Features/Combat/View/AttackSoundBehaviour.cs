using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class AttackSoundBehaviour : IEntityInit, IEntityDispose
    {
        private readonly AudioClip[] _audionClips;
        private readonly AudioSource _audioSource;
        private IEvent _fireEvent;

        public AttackSoundBehaviour(AudioClip[] attaClips, AudioSource audioSource)
        {
            _audionClips = attaClips;
            _audioSource = audioSource;
        }

        public void Init(IEntity entity)
        {
            _fireEvent = entity.GetFireEvent();
            _fireEvent.Subscribe(PlaySound);
        }

        public void Dispose(IEntity entity)
        {
            _fireEvent.Unsubscribe(PlaySound);
            _fireEvent = null;
        }

        private void PlaySound()
        {
            // A prefab with an empty clip list used to throw IndexOutOfRangeException on the
            // first attack: Random.Range(0, 0) is 0 and the array has no element 0.
            if (_audioSource == null || _audionClips == null || _audionClips.Length == 0)
                return;

            int range = Random.Range(0, _audionClips.Length);
            _audioSource.PlayOneShot(_audionClips[range]);
        }
    }
}
