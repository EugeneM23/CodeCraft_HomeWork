using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace AIKit.Entities
{
    /// <summary>
    /// A blow of AIKit content (a weapon's strike, an ability's damage — <c>DirectStriker</c>) that lands on this object
    /// reaches its entity as the <c>AIHitTaken</c> event: the game listens and turns it into its own damage. On an
    /// agent's object the character hears the blow by itself (it is a receiver too); this is the entity's side of it.
    /// No allocations per hit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityHitReceiver : MonoBehaviour, IHitReceiver
    {
        [SerializeField, Tooltip("The entity that is hit (empty — the one on this object).")]
        private MonoEntity _entity;

        private IEvent<HitInfo> _taken;

        public IEntity Entity => _entity != null ? _entity : _entity = GetComponent<MonoEntity>();

        public void ReceiveHit(in HitInfo hit)
        {
            if (_taken == null)
            {
                IEntity entity = Entity;
                if (entity == null || !entity.TryGetAIHitTaken(out _taken))
                    return;
            }

            _taken.Invoke(hit);
        }
    }
}
