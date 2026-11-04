using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public sealed class ProjectileVisualInstaller : MonoEntityInstaller
    {
        [SerializeField]
        private TrailView _trailView;
        
        public override void Install(IEntity entity)
        {
            entity.WhenEnable(() => _trailView.Show());
            entity.WhenDisable(() => _trailView.Hide());
        }
    }
}