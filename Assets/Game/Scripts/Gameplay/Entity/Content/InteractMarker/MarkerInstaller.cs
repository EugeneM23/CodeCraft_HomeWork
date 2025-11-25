using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class MarkerInstaller : SceneEntityInstaller
    {
        [SerializeField] private Vector3 _positionOffset;
        [SerializeField] private GameObject _view;

        public override void Install(IEntity entity)
        {
            entity.AddBehaviour(new InteractMarkerPositionBehaviour(_positionOffset, transform, _view));
            entity.AddBehaviour(new InteractMarkerLightBehaviour());
        }
    }
}