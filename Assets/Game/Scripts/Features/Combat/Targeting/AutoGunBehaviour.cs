using Atomic.Elements;
using Atomic.Entities;
using Unity.Profiling;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank's machine gun fires by itself (GDD §4): on the machine-gun weapon entity it aims at the nearest enemy
    /// within range (<see cref="AutoTargetTracker"/>: a search every 0.1 s, followed in between), pitch clamped, and
    /// fires whenever the tank's secondary fire condition holds (cooldown, hull intact, a driver). While the player
    /// holds the right button it fires at the cursor point instead (<see cref="CombatFocus"/>). A stunned tank does
    /// not fire. Range: the gun's <c>WeaponRange</c> (modules) when present, else <c>WeaponSettings.autoGunRange</c>.
    /// Rate: the gun's own <c>WeaponCooldown</c> inside its FireCondition — no timer here. Attached by
    /// <see cref="TargetingSetup.AttachTank"/>.
    /// With a <see cref="MachineGunRig"/> (the big gun, experiment) the gun's body turns to the target at
    /// <c>mgYawSpeed</c> / <c>mgPitchSpeed</c>, fires only once it points within <c>mgFireAngle</c>, eases back to
    /// the turret's forward with nothing to shoot, and throws a casing per shot into the <see cref="CasingField"/>.
    /// Without one only the fire point turns, at once, as before.
    /// </summary>
    public sealed class AutoGunBehaviour : IEntityInit, IEntityTick
    {
        private static readonly ProfilerMarker AutoGunMarker = new("Combat.AutoGun");

        private readonly IEntity _tank;
        private readonly AutoTargetTracker _tracker;
        private readonly float _maxPitch;
        private readonly float _yawSpeed;
        private readonly float _pitchSpeed;
        private readonly float _fireAngle;
        private readonly MachineGunRig _rig;
        private readonly CasingField _casings;

        private Transform _firePoint;
        private IExpression<bool> _condition;
        private IAction _fire;
        private IVariable<float> _range;
        private Rigidbody _tankBody;

        public AutoGunBehaviour(IEntity tank, IEnemyTargets targets, WeaponSettings settings,
            MachineGunRig rig = null, CasingField casings = null)
        {
            _tank = tank;
            _maxPitch = settings.maxPitch;
            _yawSpeed = settings.mgYawSpeed;
            _pitchSpeed = settings.mgPitchSpeed;
            _fireAngle = settings.mgFireAngle;
            _rig = rig;
            _casings = casings;
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

            // Casings leave with the tank's own speed.
            _tank.TryGetRiggedBody(out _tankBody);
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

            Vector3 from = _rig != null ? _rig.Pivot.position : _firePoint.position;
            if (!_tracker.TryGetAimPoint(from, deltaTime, out Vector3 aim))
            {
                _rig?.Rest(deltaTime, _yawSpeed, _pitchSpeed);
                return;
            }

            if (_rig != null)
            {
                if (!_rig.Turn(aim, _maxPitch, deltaTime, _yawSpeed, _pitchSpeed, _fireAngle))
                    return; // still turning to it
            }
            else if (TargetGeometry.TryAim(from, aim, _maxPitch, out Quaternion rotation))
            {
                _firePoint.rotation = rotation;
            }

            if (_condition != null && !_condition.Invoke())
                return;

            _fire.Invoke();
            EjectCasing();
        }

        private void EjectCasing()
        {
            if (_casings == null || _rig == null)
                return;

            Transform ejector = _rig.Ejector;
            Vector3 inherited = _tankBody != null ? _tankBody.linearVelocity : Vector3.zero;
            _casings.Eject(ejector.position, ejector.rotation, inherited);
        }
    }
}
