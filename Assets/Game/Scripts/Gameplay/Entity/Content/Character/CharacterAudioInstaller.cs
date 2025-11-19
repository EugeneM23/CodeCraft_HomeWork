using Atomic.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay
{
    public sealed class CharacterAudioInstaller : SceneEntityInstaller
    {
        [SerializeField]
        private AudioSource _audioSource;
        
        [FormerlySerializedAs("_damageLevels")]
        [SerializeField]
        private PainSoundBehaviour.Level[] _painLevels;

        [SerializeField]
        private AudioClip[] _deathClips;

        [SerializeField]
        private AudioClip[] _moveStepClips;

        [SerializeField]
        private AudioClip _bodyFallClip;

        [SerializeField]
        private AudioClip _meleeDamageClip;

        [SerializeField]
        private AudioClip _bulletDamageClip;
        
        public override void Install(IEntity entity)
        {
            // TODO

        }
    }
}