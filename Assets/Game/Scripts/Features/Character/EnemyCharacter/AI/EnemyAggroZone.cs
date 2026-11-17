using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(Collider))]
    public sealed class EnemyAggroZone : MonoBehaviour
    {
        [SerializeField] private MonoEntity[] _enemies;

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetEntity(out IEntity entity) && entity.HasPlayerTag())
                this.SetTarget(entity);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetEntity(out IEntity entity) && entity.HasPlayerTag())
                this.SetTarget(null);
        }

        /// <remarks>
        /// A killed enemy stays in the list: DeathBehaviour disposes it, which strips its values,
        /// and GetTarget on it threw KeyNotFoundException. That aborted the loop, so every enemy
        /// after a dead one in the list stopped noticing the player. Dead and empty entries are
        /// skipped now.
        /// </remarks>
        private void SetTarget(IEntity target)
        {
            foreach (MonoEntity enemy in _enemies)
            {
                if (enemy != null && enemy.TryGetTarget(out IReactiveVariable<IEntity> slot))
                    slot.Value = target;
            }
        }
    }
}
