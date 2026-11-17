using AIKit;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// A blow AIKit delivers (<c>DirectStriker</c>: an AIKit weapon or ability asset) reaches the game's health: the hit
    /// becomes a <c>DamageUseCase.TakeDamage</c> on this object's entity, from a source entity that carries the hit's
    /// damage and the attacker's transform. Not on the path of the game's own weapons (they call DamageUseCase
    /// themselves); here so that a blow given by AIKit content is never lost. No allocations per hit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AtomicHitReceiver : MonoBehaviour, IHitReceiver
    {
        [SerializeField, Tooltip("The entity whose health takes the hits (empty — the one on this object).")]
        private MonoEntity _entity;

        private Entity _source;
        private IReactiveVariable<int> _damage;
        private TakeDamageArgs _args;

        /// <summary>The source of the hits this receiver passes on (null until the first): its own listeners tell them apart.</summary>
        public IEntity Source => _source;

        /// <summary>The entity the hits go to.</summary>
        public IEntity Target => _entity != null ? _entity : _entity = GetComponent<MonoEntity>();

        public void ReceiveHit(in HitInfo hit)
        {
            IEntity target = Target;
            if (target == null || hit.Damage <= 0f)
                return;

            if (_source == null)
            {
                _source = new Entity("AIKitHit");
                _damage = new ReactiveVariable<int>(0);
                _source.AddDamage(_damage);
                _source.AddTransform(transform);
                _args = new TakeDamageArgs(_source);
            }

            _source.SetTransform(hit.Attacker != null ? hit.Attacker : transform);
            _damage.Value = Mathf.Max(1, Mathf.RoundToInt(hit.Damage));
            DamageUseCase.TakeDamage(target, _args);
        }
    }
}
