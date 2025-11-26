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
            entity.AddTransform(transform);
            entity.AddPositionOffset(_positionOffset);
            entity.AddBehaviour(new InteractMarkerHighlightBehaviour());
            entity.AddBehaviour(new InteractMarkerBehaviour(_view, GameContext.Instance));
        }
    }
}