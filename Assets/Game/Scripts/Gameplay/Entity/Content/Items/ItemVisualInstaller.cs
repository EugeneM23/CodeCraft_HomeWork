using Atomic.Entities;
using HighlightPlus;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay
{
    public sealed class ItemVisualInstaller : SceneEntityInstaller
    {
        [SerializeField] private ParticleSystem _vfx;
        [SerializeField] private HighlightEffect _highlight;
        [SerializeField] private GameObject _visual;
        [SerializeField] private AudioSource _audioSource;

        [SerializeField] private AudioClip _pickUpSound;
        [SerializeField] private AudioClip _dropSound;

        private IEntityPool _audioPool;

        public override void Install(IEntity entity)
        {
            entity.AddHighlight(_highlight);
            entity.GetPickUpEvent().OnEvent += () =>
            {
                IEntity go = GameContext.Instance.GetGameFactory().Create(GameFactoryID.AudioSource.ToString());
                go.GetLifeTime().Reset();
                go.GetAudioSource().PlayOneShot(_pickUpSound);
            };

            if (entity.TryGetDropEvent(out var @event))
                @event.OnEvent += () => { _audioSource.PlayOneShot(_dropSound); };
        }
    }
}