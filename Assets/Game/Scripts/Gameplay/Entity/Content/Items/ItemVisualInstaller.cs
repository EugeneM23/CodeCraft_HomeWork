using Atomic.Entities;
using HighlightPlus;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class ItemVisualInstaller : SceneEntityInstaller
    {
        [SerializeField] private ParticleSystem _vfx;
        [SerializeField] private HighlightEffect _highlight;
        [SerializeField] private GameObject _visual;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _audioClip;

        private IEntityPool _audioPool;

        public override void Install(IEntity entity)
        {
            entity.AddHighlight(_highlight);
            entity.GetPickUpEvent().OnEvent += () =>
            {
                IEntity go = GameContext.Instance.GetAudioPool().Rent();
                go.GetLifeTime().Reset();
                go.GetAudioSource().PlayOneShot(_audioClip);
            };
        }
    }
}