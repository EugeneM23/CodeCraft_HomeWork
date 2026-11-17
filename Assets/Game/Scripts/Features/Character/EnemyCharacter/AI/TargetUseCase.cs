using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class TargetUseCase
    {
        /// <summary>
        /// Resolves the target's transform, forgetting the target if it is gone.
        /// </summary>
        /// <remarks>
        /// A target does not stop existing when it dies: DeathBehaviour disposes the entity, which
        /// strips its values but leaves the object, so the reference an enemy holds stays
        /// non-null while GetTransform on it throws KeyNotFoundException. Until enemies could
        /// acquire a target at all this never came up; the first time one chased a player to
        /// death, it threw every frame from then on.
        ///
        /// Clearing the variable rather than only returning false is what lets the enemy settle:
        /// EnemyCharacterPatrolBehaviour stands down while a target is set, so a stale one would leave the
        /// enemy neither chasing nor patrolling.
        /// </remarks>
        public static bool TryResolve(IReactiveVariable<IEntity> target, out Transform transform)
        {
            transform = null;

            IEntity value = target.Value;

            if (value == null)
                return false;

            if (value.TryGetTransform(out transform))
                return true;

            target.Value = null;
            return false;
        }
    }
}
