using UnityEngine;

namespace Gameplay.Controllers.AttackAction
{
    public class DealDamageAttackAction : AttackComponent.IHitAction
    {
        private readonly int _damage;

        public DealDamageAttackAction(int damage)
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