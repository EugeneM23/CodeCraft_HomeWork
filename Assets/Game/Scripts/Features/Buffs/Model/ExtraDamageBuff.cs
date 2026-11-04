using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "ExtraDamageBuffConfig", menuName = "Gameplay/ExtraDamageBuffConfig")]
    public class ExtraDamageBuff : TemporaryBuff
    {
        private readonly int _extraDamage;

        /// <summary>
        /// The weapon the bonus was actually applied to.
        /// </summary>
        /// <remarks>
        /// OnDiscard used to re-read entity.GetWeapon().Value. Weapon is reassigned on every
        /// pick-up and the weapon entities are catalog singletons shared across pick-ups, so
        /// picking up a weapon while this buff was active subtracted the bonus from the new weapon
        /// and doubled its cooldown, while the old one kept the extra damage and the halved
        /// cooldown permanently.
        /// </remarks>
        private IEntity _buffedWeapon;

        public ExtraDamageBuff(ExtraDamageBuffConfig config) : base(config)
        {
            _extraDamage = config.Damage;
        }

        protected override bool IsValid(IEntity entity) =>
            entity.HasWeapon() && entity.GetWeapon().Value != null;

        protected override void OnApply(IEntity entity)
        {
            _buffedWeapon = entity.GetWeapon().Value;

            _buffedWeapon.GetDamage().Value += _extraDamage;

            CooldownTimer cooldown = _buffedWeapon.GetWeaponCooldown();
            cooldown.SetDuration(cooldown.Duration / 2f);
        }

        protected override void OnDiscard(IEntity entity)
        {
            // Discard now runs even when the character no longer holds a weapon, so the undo has
            // to stand on its own.
            if (_buffedWeapon == null)
                return;

            _buffedWeapon.GetDamage().Value -= _extraDamage;

            CooldownTimer cooldown = _buffedWeapon.GetWeaponCooldown();
            cooldown.SetDuration(cooldown.Duration * 2f);

            _buffedWeapon = null;
        }
    }
}
