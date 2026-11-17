using System;
using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Follows a weapon slot and reports every shot of the weapon in it.
    /// </summary>
    /// <remarks>
    /// An attack raises the FireEvent of the weapon, not of whoever holds it, and the weapon can be
    /// replaced while the holder lives. The attack animations, the attack sound and the camera
    /// shake each used to re-subscribe by hand, and the copies drifted: only some survived a weapon
    /// destroyed first when the scene unloads.
    /// </remarks>
    public sealed class HeldWeaponFireListener : IDisposable
    {
        private readonly Action<IEntity> _onFire;
        private readonly Action<IEntity> _onWeaponChanged;
        private readonly Subscription<IEntity> _slotSubscription;

        private IEntity _weapon;

        /// <param name="slot">The holder's Weapon value.</param>
        /// <param name="onFire">Called with the weapon that fired.</param>
        /// <param name="onWeaponChanged">Called with the new weapon, or null for empty hands —
        /// also once straight away for the weapon held now.</param>
        public HeldWeaponFireListener(
            IReactiveVariable<IEntity> slot,
            Action<IEntity> onFire,
            Action<IEntity> onWeaponChanged = null)
        {
            _onFire = onFire;
            _onWeaponChanged = onWeaponChanged;

            // Observe invokes straight away, so the weapon held now is picked up too.
            _slotSubscription = slot.Observe(this.OnWeaponChanged);
        }

        public void Dispose()
        {
            _slotSubscription.Dispose();
            this.Release();
        }

        private void OnWeaponChanged(IEntity weapon)
        {
            this.Release();

            _weapon = weapon;

            if (_weapon != null && _weapon.TryGetFireEvent(out IEvent fire))
                fire.Subscribe(this.OnFire);

            _onWeaponChanged?.Invoke(weapon);
        }

        private void Release()
        {
            // On teardown Unity destroys objects in no fixed order, so the weapon may be gone
            // already. A destroyed MonoEntity is still a non-null IEntity; IsValid is the check
            // that survives that.
            if (_weapon is { IsValid: true } && _weapon.TryGetFireEvent(out IEvent fire))
                fire.Unsubscribe(this.OnFire);

            _weapon = null;
        }

        private void OnFire() => _onFire(_weapon);
    }
}
