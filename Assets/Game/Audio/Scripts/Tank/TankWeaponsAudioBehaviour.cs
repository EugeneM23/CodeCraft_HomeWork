using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// The tank's guns: the cannon shot and, once its cooldown runs out after a shot, the reload clank; the
    /// machine gun's burst. Follows the Weapon and SecondaryWeapon slots, so a swapped gun is heard too. A gun
    /// that still has its old shot sound (<see cref="LegacySoundUseCase"/>) gets no new one.
    /// </summary>
    public sealed class TankWeaponsAudioBehaviour : IEntityInit, IEntityTick, IEntityDispose
    {
        /// <summary>Cooldowns shorter than this (s) are a rate of fire, not a reload.</summary>
        private const float MIN_RELOAD = 0.5f;

        private readonly Action<IEntity> _onPrimaryChanged;
        private readonly Action<IEntity> _onSecondaryChanged;
        private readonly Action _onPrimaryFire;
        private readonly Action _onSecondaryFire;

        private Transform _hull;
        private Subscription<IEntity> _primarySlot;
        private Subscription<IEntity> _secondarySlot;
        private bool _hasPrimarySlot;
        private bool _hasSecondarySlot;

        private IEntity _primary;
        private IEvent _primaryFire;
        private Transform _primaryMuzzle;
        private CooldownTimer _cooldown;
        private bool _awaitingReload;
        private bool _primaryLegacy;

        private IEntity _secondary;
        private IEvent _secondaryFire;
        private Transform _secondaryMuzzle;
        private bool _secondaryLegacy;

        public TankWeaponsAudioBehaviour()
        {
            _onPrimaryChanged = OnPrimaryChanged;
            _onSecondaryChanged = OnSecondaryChanged;
            _onPrimaryFire = OnPrimaryFire;
            _onSecondaryFire = OnSecondaryFire;
        }

        public void Init(IEntity entity)
        {
            _hull = entity.GetTransform();

            // Observe calls back at once with the gun mounted now.
            if (entity.TryGetWeapon(out IReactiveVariable<IEntity> primary))
            {
                _primarySlot = primary.Observe(_onPrimaryChanged);
                _hasPrimarySlot = true;
            }

            if (entity.TryGetSecondaryWeapon(out IReactiveVariable<IEntity> secondary))
            {
                _secondarySlot = secondary.Observe(_onSecondaryChanged);
                _hasSecondarySlot = true;
            }
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (!_awaitingReload || _cooldown == null || !_cooldown.IsExpired())
                return;

            _awaitingReload = false;
            GameAudio.Play(AudioKeys.Weapons.CANNON_RELOAD, MuzzleOf(_primaryMuzzle));
        }

        public void Dispose(IEntity entity)
        {
            if (_hasPrimarySlot)
                _primarySlot.Dispose();
            if (_hasSecondarySlot)
                _secondarySlot.Dispose();
            _hasPrimarySlot = _hasSecondarySlot = false;

            Release(ref _primary, ref _primaryFire, _onPrimaryFire);
            Release(ref _secondary, ref _secondaryFire, _onSecondaryFire);
        }

        private void OnPrimaryChanged(IEntity weapon)
        {
            Release(ref _primary, ref _primaryFire, _onPrimaryFire);
            _primaryMuzzle = null;
            _cooldown = null;
            _awaitingReload = false;

            if (weapon == null)
                return;

            _primary = weapon;
            _primaryMuzzle = FindMuzzle(weapon);
            _primaryLegacy = LegacySoundUseCase.HasLegacyShotSound(weapon);
            if (weapon.TryGetWeaponCooldown(out CooldownTimer cooldown) && cooldown.Duration >= MIN_RELOAD)
                _cooldown = cooldown;
            Listen(weapon, ref _primaryFire, _onPrimaryFire);
        }

        private void OnSecondaryChanged(IEntity weapon)
        {
            Release(ref _secondary, ref _secondaryFire, _onSecondaryFire);
            _secondaryMuzzle = null;

            if (weapon == null)
                return;

            _secondary = weapon;
            _secondaryMuzzle = FindMuzzle(weapon);
            _secondaryLegacy = LegacySoundUseCase.HasLegacyShotSound(weapon);
            Listen(weapon, ref _secondaryFire, _onSecondaryFire);
        }

        private void OnPrimaryFire()
        {
            // The reload is new; only the shot can be doubled by the old sound.
            if (!_primaryLegacy)
                GameAudio.Play(AudioKeys.Weapons.CANNON_SHOT, MuzzleOf(_primaryMuzzle));
            _awaitingReload = _cooldown != null;
        }

        private void OnSecondaryFire()
        {
            if (!_secondaryLegacy)
                GameAudio.Play(AudioKeys.Weapons.MG_SHOT, MuzzleOf(_secondaryMuzzle));
        }

        private Vector3 MuzzleOf(Transform muzzle) => muzzle != null ? muzzle.position : _hull.position;

        private static Transform FindMuzzle(IEntity weapon)
        {
            if (weapon.TryGetFirePoint(out Transform firePoint) && firePoint != null)
                return firePoint;
            return weapon.TryGetTransform(out Transform transform) ? transform : null;
        }

        private static void Listen(IEntity weapon, ref IEvent fire, Action handler)
        {
            if (weapon.TryGetFireEvent(out fire))
                fire.OnEvent += handler;
        }

        private static void Release(ref IEntity weapon, ref IEvent fire, Action handler)
        {
            // On teardown the weapon may be destroyed first; a destroyed MonoEntity is still a non-null IEntity.
            if (fire != null && weapon is { IsValid: true })
                fire.OnEvent -= handler;

            fire = null;
            weapon = null;
        }
    }
}
