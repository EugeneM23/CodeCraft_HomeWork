using Atomic.Elements;
using Atomic.Entities;
using Unity.Profiling;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank's machine gun fires by itself (GDD §4): on the machine-gun weapon entity it turns the fire point to
    /// the nearest enemy within range (<see cref="AutoTargetTracker"/>: a search every 0.1 s, followed in between),
    /// pitch clamped, and fires whenever the tank's secondary fire condition holds (cooldown, hull intact, a driver).
    /// While the player holds the right button it fires at the cursor point instead (<see cref="CombatFocus"/>).
    /// A stunned tank does not fire. Range: the gun's <c>WeaponRange</c> (modules) when present, else
    /// <c>WeaponSettings.autoGunRange</c>. Rate: the gun's own <c>WeaponCooldown</c> inside its FireCondition — no
    /// timer here. Attached by <see cref="TargetingSetup.AttachTank"/>.
    /// </summary>
    public sealed class AutoGunBehaviour : IEntityInit, IEntityTick
    {
        private static readonly ProfilerMarker AutoGunMarker = new("Combat.AutoGun");

        private readonly IEntity _tank;
        private readonly AutoTargetTracker _tracker;
        private readonly float _maxPitch;

        private Transform _firePoint;
        private IExpression<bool> _condition;
        private IAction _fire;
        private IVariable<float> _range;

        public AutoGunBehaviour(IEntity tank, IEnemyTargets targets, WeaponSettings settings)
        {
            _tank = tank;
            _maxPitch = settings.maxPitch;
            _tracker = new AutoTargetTracker(settings.autoGunRange, settings.retargetInterval);
            _tracker.Bind(targets);
        }

        public void Init(IEntity entity)
        {
            entity.TryGetFirePoint(out _firePoint);

            // The loadout's range (modules area) when the gun has one; WeaponSettings.autoGunRange otherwise.
            entity.TryGetWeaponRange(out _range);

            // The tank's own condition and action wrap the gun's with "hull intact, somebody drives".
            if (!_tank.TryGetSecondaryFireCondition(out _condition))
                entity.TryGetFireCondition(out _condition);
            if (!_tank.TryGetSecondaryFireAction(out _fire))
                entity.TryGetFireAction(out _fire);
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_firePoint == null || _fire == null || TankStunUseCase.IsStunned(_tank))
                return;

            using (AutoGunMarker.Auto())
                Aim(deltaTime);
        }

        private void Aim(float deltaTime)
        {
            if (_range != null && _range.Value > 0f)
                _tracker.Range = _range.Value;

            Vector3 muzzle = _firePoint.position;
            if (!_tracker.TryGetAimPoint(muzzle, deltaTime, out Vector3 aim))
                return;

            if (TargetGeometry.TryAim(muzzle, aim, _maxPitch, out Quaternion rotation))
                _firePoint.rotation = rotation;

            if (_condition == null || _condition.Invoke())
                _fire.Invoke();
        }
    }
}
