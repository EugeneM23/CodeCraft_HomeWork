using Atomic.Elements;
using Atomic.Entities;
using Atomic.Extensions;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay
{
    [CreateAssetMenu(fileName = "DamageAspect", menuName = "Gameplay/DamageAspect")]
    public class DamageAspect : ScriptableEntityAspect
    {
        [SerializeField] private int _extraDamage;

        public override void Apply(IEntity entity)
        {
            if (entity.TryGetWeapon(out var weapon))
            {
                weapon.Value.GetDamage().Value += _extraDamage;
            }
        }

        public override void Discard(IEntity entity)
        {
            if (entity.TryGetWeapon(out var weapon))
                weapon.Value.GetDamage().Value -= _extraDamage;
        }
    }
}