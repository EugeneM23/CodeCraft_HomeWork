using Atomic.Elements;
using Atomic.Entities;
using TMPro;
using UnityEngine;

namespace Game
{
    public class EnemyCharacterAttackBehaviour : IEntityInit, IEntityTick
    {
        private const float ATTACK_DISTANCE = 1;
        private IReactiveVariable<IEntity> _target;
        private Transform _enemy;

        public void Init(IEntity entity)
        {
            _target = entity.GetTarget();
            _enemy = entity.GetTransform();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            // Same guard as the chase: a dead target is disposed but still referenced, and asking
            // it for a transform throws.
            if (!TargetUseCase.TryResolve(_target, out Transform targetTransform)) return;

            float distance = DistanceTo(_target.Value, targetTransform, _enemy.position);

            if (distance < ATTACK_DISTANCE)
            {
                if (entity.GetWeapon().Value.GetFireCondition().Invoke())
                    entity.GetFireAction().Invoke();

            }
        }

        /// <summary>
        /// How far the enemy is from what it can hit. For a person that is the distance to their
        /// position. A vehicle is far wider than the attack distance: its centre lies more than a
        /// metre inside its own hull, so an enemy pressed against the side would never swing.
        /// It is measured to the nearest point of the vehicle's colliders instead.
        /// </summary>
        private static float DistanceTo(IEntity target, Transform targetTransform, Vector3 from)
        {
            if (target.HasVehicleTag() && target.TryGetRiggedBody(out Rigidbody body) && body != null)
                return Vector3.Distance(body.ClosestPointOnBounds(from), from);

            return Vector3.Distance(targetTransform.position, from);
        }
    }
}