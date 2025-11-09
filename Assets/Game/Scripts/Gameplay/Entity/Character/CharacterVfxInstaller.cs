using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class CharacterVfxInstaller : SceneEntityInstaller
    {
        [SerializeField]
        private ParticleSystem _bulletBlood;
        
        [SerializeField]
        private ParticleSystem _meleeBlood;

        [SerializeField]
        private ParticleSystem _deadBlood;

        [SerializeField]
        private Transform _groundPoint;

        [SerializeField]
        private Transform _rootTransform;

        public override void Install(IEntity entity)
        {
            // TODO
        }
    }
}