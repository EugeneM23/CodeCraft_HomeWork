using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "ExtraDamageBuffConfig", menuName = "Gameplay/ExtraDamageBuffConfig")]
    public class ExtraDamageBuff : TemporaryBuff
    {
        private readonly int _extraDamage;

        public ExtraDamageBuff(ExtraDamageBuffConfig config) : base(config)
        {
            _extraDamage = config.Damage;
        }

        protected override bool IsValid(IEntity entity) => entity.HasWeapon();

        protected override void OnApply(IEntity entity)
        {
            IEntity weapon = entity.GetWeapon().Value;
            weapon.GetDamage().Value += _extraDamage;

            Cooldown cooldown = weapon.GetWeaponCooldown();
            cooldown.SetDuration(cooldown.Duration / 2f);
        }

        protected override void OnDiscard(IEntity entity)
        {
            IEntity weapon = entity.GetWeapon().Value;
            weapon.GetDamage().Value -= _extraDamage;

            Cooldown cooldown = weapon.GetWeaponCooldown();
            cooldown.SetDuration(cooldown.Duration * 2f);
        }
    }
}