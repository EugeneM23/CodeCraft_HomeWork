using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class LookAtBehaviour : IEntityInit, IEntityUpdate
    {
        private Transform _target;

        public void Init(in IEntity entity)
        {
            _target = entity.GetTarget();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            float distance = Vector3.Distance(_target.position, entity.GetTransform().position);

            if (distance < 5)
            {
                Vector3 direction = (_target.position - entity.GetTransform().position).normalized;
                entity.Rotate(direction, deltaTime);
            }
        }
    }
}