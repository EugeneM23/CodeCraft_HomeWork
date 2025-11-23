using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class ItemVisualInstaller : SceneEntityInstaller
    {
        [SerializeField] private ParticleSystem _vfx;

        [SerializeField] private GameObject _visual;

        [SerializeField] private AudioSource _audioSource;

        [SerializeField] private AudioClip _audioClip;

        private IEntityPool _audioPool;

        public override void Install(IEntity entity)
        {
            entity.GetPickUpEvent().OnEvent += () =>
            {
                IEntity go = GameContext.Instance.GetAudioPool().Rent();
                go.GetLifeTime().Reset();
                go.GetAudioSource().PlayOneShot(_audioClip);
            };
        }
    }
}