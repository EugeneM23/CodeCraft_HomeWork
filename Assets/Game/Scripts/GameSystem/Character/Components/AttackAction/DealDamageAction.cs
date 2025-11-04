using UnityEngine;

namespace Gameplay.Controllers.AttackAction
{
    public class DealDamageAction : AttackComponent.IEntityHitAction
    {
        private readonly int _damage;

        public DealDamageAction(int damage)
        {
            _damage = damage;
        }

        public void Invoke(RaycastHit2D hit, Entity entity)
        {
            IDamageable component = entity.GetEntityComponent<IDamageable>();
            component.TakeDamage(_damage);
        }
    }
}