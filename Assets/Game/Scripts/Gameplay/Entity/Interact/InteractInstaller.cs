using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class InteractInstaller : IEntityInstaller
    {
        [SerializeField] private Transform _center;
        [SerializeField] private float _radius;
        [SerializeField] private LayerMask _layerMask;

        private QueryTriggerInteraction _triggerInteraction;

        public void Install(IEntity entity)
        {
            entity.AddBehaviour(new DetectInteractableBehaviour(_center, _layerMask, _radius, _triggerInteraction));
            entity.AddBehaviour<ShowInteractUIBehaviour>();
            entity.AddTargetInteractable(new ReactiveVariable<IEntity>());
        }
    }
}