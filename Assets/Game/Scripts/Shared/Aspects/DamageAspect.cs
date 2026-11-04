using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "DamageAspect", menuName = "BuffSystem/Aspect/DamageAspect")]
    public class DamageAspect : ScriptableEntityAspect
    {
        [SerializeField] private int _extraDamage;

        // The weapon slot can be empty — dropping the weapon with G inside an effect area used to
        // be enough — and a weapon entity that is being torn down has already cleared its values.
        // Either case threw here, and a throw inside a composite aspect skips every aspect after
        // this one, which left the entity half-buffed.
        public override void Apply(IEntity entity)
        {
            if (TryGetDamage(entity, out IReactiveVariable<int> damage))
                damage.Value += _extraDamage;
        }

        public override void Discard(IEntity entity)
        {
            if (TryGetDamage(entity, out IReactiveVariable<int> damage))
                damage.Value -= _extraDamage;
        }

        private static bool TryGetDamage(IEntity entity, out IReactiveVariable<int> damage)
        {
            damage = null;

            return entity.TryGetWeapon(out IReactiveVariable<IEntity> weapon)
                   && weapon.Value is { IsValid: true }
                   && weapon.Value.TryGetDamage(out damage);
        }
    }
}
