using System.Buffers;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class DetectInteractableBehaviour : IEntityFixedUpdate, IEntityInit
    {
        private IReactiveVariable<IEntity> _target;

        private readonly Transform _center;
        private readonly LayerMask _layerMask;
        private readonly float _radius;
        private readonly QueryTriggerInteraction _triggerInteraction;

        public DetectInteractableBehaviour(
            Transform center,
            LayerMask layerMask,
            float radius,
            QueryTriggerInteraction triggerInteraction
        )
        {
            _center = center;
            _layerMask = layerMask;
            _radius = radius;
            _triggerInteraction = triggerInteraction;
        }

        public void Init(in IEntity entity)
        {
            _target = entity.GetTargetInteractable();
        }

        public void OnFixedUpdate(in IEntity entity, in float deltaTime)
        {
            InteractUseCase.FindClosestItem(_center, _radius, _layerMask, _triggerInteraction, out IEntity target);
            _target.Value = target;
        }
    }
}