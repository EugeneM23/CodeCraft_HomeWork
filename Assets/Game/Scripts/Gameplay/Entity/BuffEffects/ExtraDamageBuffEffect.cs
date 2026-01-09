using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "ExtraDamageBuffEffect", menuName = "Gameplay/ExtraDamageBuffEffect")]
    public class ExtraDamageBuff : BaseBuff
    {
        [SerializeField] private int _extraDamage;
 
        public override void Apply(IEntity entity)
        {
            if (entity.TryGetWeapon(out var weapon)) 
                weapon.Value.GetDamage().Value += _extraDamage;
        }

        public override void Discard(IEntity entity)
        {
            if (entity.TryGetWeapon(out var weapon))
                weapon.Value.GetDamage().Value -= _extraDamage;
        }
    }
}