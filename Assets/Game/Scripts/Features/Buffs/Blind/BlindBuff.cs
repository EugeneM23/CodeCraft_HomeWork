using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A flash blinded the enemy: it loses its target and stumbles about at random instead of
    /// patrolling, chasing or attacking. When its sight comes back it resumes what it was doing,
    /// chasing the same target if that is still alive.
    /// </summary>
    /// <remarks>
    /// Removes and restores the AI behaviours the way StunBuff does, but leaves movement itself on:
    /// a blind enemy moves, it just cannot tell where it is going.
    /// </remarks>
    public class BlindBuff : TemporaryBuff
    {
        private readonly Dictionary<Type, IEntityBehaviour> _behaviours = new();
        private readonly BlindStumbleBehaviour _stumble;
        private IEntity _target;

        public BlindBuff(BlindBuffConfig config) : base(config) =>
            _stumble = new BlindStumbleBehaviour(config.StumbleSpeed);

        protected override bool IsValid(IEntity entity) =>
            entity.HasTarget() && entity.HasMoveDirection() && entity.HasRotateDirection();

        protected override void OnApply(IEntity entity)
        {
            IReactiveVariable<IEntity> target = entity.GetTarget();
            _target = target.Value;
            target.Value = null;

            CollectAndRemove<EnemyCharacterChaseBehaviour>(entity);
            CollectAndRemove<EnemyCharacterAttackBehaviour>(entity);
            CollectAndRemove<EnemyCharacterPatrolBehaviour>(entity);

            entity.AddBehaviour(_stumble);
        }

        protected override void OnDiscard(IEntity entity)
        {
            entity.DelBehaviour(_stumble);

            // Read through TryGet: the buff is also discarded on death, after which the values are gone.
            if (entity.TryGetMoveDirection(out IReactiveVariable<Vector3> move))
                move.Value = Vector3.zero;

            foreach (IEntityBehaviour behaviour in _behaviours.Values)
                entity.AddBehaviour(behaviour);

            _behaviours.Clear();

            // Sight is back: the enemy finds what it was after, unless someone else gave it a target
            // meanwhile or the old one died.
            if (entity.TryGetTarget(out IReactiveVariable<IEntity> target)
                && target.Value == null && _target != null && _target.TryGetTransform(out _))
                target.Value = _target;

            _target = null;
        }

        private void CollectAndRemove<T>(IEntity entity)
            where T : class, IEntityBehaviour
        {
            if (!entity.HasBehaviour<T>())
                return;

            _behaviours[typeof(T)] = entity.GetBehaviour<T>();
            entity.DelBehaviour<T>();
        }
    }
}
