using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class ItemVisualInstaller : SceneEntityInstaller
    {
        [SerializeField]
        private ParticleSystem _vfx;

        [SerializeField]
        private GameObject _visual;

        [SerializeField]
        private AudioSource _audioSource;
        
        public override void Install(IEntity entity)
        {
            //TODO
        }
    }
}