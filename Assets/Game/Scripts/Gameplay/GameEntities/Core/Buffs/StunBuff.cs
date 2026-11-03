using System;
using System.Collections.Generic;
using Atomic.Entities;
using Game.Gameplay;

namespace Game
{
    public class StunBuff : TemporaryBuff
    {
        private readonly Dictionary<Type, IEntityBehaviour> _behaviours = new();

        public StunBuff(BuffConfig config) : base(config) { }

        protected override bool IsValid(IEntity entity) => true;

        protected override void OnApply(IEntity entity)
        {
            entity.GetTransform().localScale *= 1.5f;

            CollectAndRemove<CharacterMoveBehaviour>(entity);
            CollectAndRemove<CharacterRotateBehaviour>(entity);
            CollectAndRemove<CharacterMoveAnimBehaviour>(entity);
            CollectAndRemove<EnemyPatrolBehaviour>(entity);
            CollectAndRemove<EnemyChaseBehaviour>(entity);
            CollectAndRemove<EnemyAttackBehaviour>(entity);

            entity.GetAnimator().SetBool("IsMoving", false);
        }

        protected override void OnDiscard(IEntity entity)
        {
            foreach (var behaviour in _behaviours.Values) 
                entity.AddBehaviour(behaviour);

            _behaviours.Clear();

            entity.GetTransform().localScale /= 1.5f;
        }

        private void CollectAndRemove<T>(IEntity entity)
            where T : class, IEntityBehaviour
        {
            if (!entity.HasBehaviour<T>())
                return;

            var behaviour = entity.GetBehaviour<T>();
            _behaviours[typeof(T)] = behaviour;
            entity.DelBehaviour<T>();
        }
    }
}